using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    public static class PawOrderOutcomeEvidenceTool
    {
        private static readonly string[] Suspects = { "Greta", "Vera", "Milo", "Nina", "Otto", "Boris" };
        private const string OutcomesRoot = "Assets/Project/Scriptables/Investigations/PawOrder/Outcomes";
        private const string InteractionsRoot = "Assets/Project/Scriptables/Investigations/PawOrder/Interactions";
        private const string PreviewCsvPath = "Assets/Project/Scriptables/Investigations/PawOrder/Outcomes/PawOrder_OutcomeEvidencePreview.csv";
        private const string PreviewJsonPath = "Assets/Project/Scriptables/Investigations/PawOrder/Outcomes/PawOrder_OutcomeEvidencePreview.json";

        [MenuItem("Tools/Paw Order/Investigations/Generate Evidence Preview (CSV+JSON)")]
        public static void GeneratePreviewOnly()
        {
            Process(applyToAssets: false, overwriteExisting: false);
        }

        [MenuItem("Tools/Paw Order/Investigations/Generate Evidence Preview + Apply")]
        public static void GeneratePreviewAndApply()
        {
            Process(applyToAssets: true, overwriteExisting: false);
        }

        [MenuItem("Tools/Paw Order/Investigations/Generate Evidence Preview + Apply (Overwrite Existing)")]
        public static void GeneratePreviewAndApplyOverwrite()
        {
            Process(applyToAssets: true, overwriteExisting: true);
        }

        public static void RunFromBatchModePreviewAndApply()
        {
            Process(applyToAssets: true, overwriteExisting: false);
        }

        private static void Process(bool applyToAssets, bool overwriteExisting)
        {
            Dictionary<string, CharacterData> suspects = LoadSuspects();
            Dictionary<string, InteractionContext> contextsByOutcome = LoadInteractionContexts();
            string[] outcomeGuids = AssetDatabase.FindAssets("t:InteractionOutcomeData", new[] { OutcomesRoot });
            List<string> csvLines = new() { "outcomeId,question,targetCharacter,suspect,motivationScore,meansScore,opportunityScore,score,rationale" };
            List<ScoreRow> allRows = new();

            int updated = 0;
            int skippedBecauseHasValues = 0;

            foreach (string guid in outcomeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                InteractionOutcomeData outcome = AssetDatabase.LoadAssetAtPath<InteractionOutcomeData>(path);
                if (outcome == null || string.IsNullOrWhiteSpace(outcome.OutcomeId))
                {
                    continue;
                }

                InteractionContext context = contextsByOutcome.TryGetValue(outcome.OutcomeId, out InteractionContext foundContext)
                    ? foundContext
                    : BuildFallbackContext(outcome.OutcomeId);

                string response = outcome.ResponseText ?? string.Empty;
                string responseLower = response.ToLowerInvariant();

                List<ScoreRow> rows = new();
                foreach (string suspect in Suspects)
                {
                    ScoreRow row = BuildRow(outcome, context, suspect, responseLower);
                    rows.Add(row);
                    allRows.Add(row);
                    csvLines.Add(ToCsv(row));
                }

                if (!applyToAssets)
                {
                    continue;
                }

                if (outcome.SuspectEvidenceValues.Count > 0 && !overwriteExisting)
                {
                    skippedBecauseHasValues++;
                    continue;
                }

                SerializedObject so = new(outcome);
                SerializedProperty values = so.FindProperty("suspectEvidenceValues");
                values.arraySize = 0;

                for (int i = 0; i < Suspects.Length; i++)
                {
                    values.InsertArrayElementAtIndex(i);
                    SerializedProperty entry = values.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("suspect").objectReferenceValue = suspects[Suspects[i]];
                    entry.FindPropertyRelative("score").intValue = rows[i].Score;
                    entry.FindPropertyRelative("motivationScore").intValue = rows[i].MotivationScore;
                    entry.FindPropertyRelative("meansScore").intValue = rows[i].MeansScore;
                    entry.FindPropertyRelative("opportunityScore").intValue = rows[i].OpportunityScore;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(outcome);
                updated++;
            }

            File.WriteAllLines(PreviewCsvPath, csvLines, new UTF8Encoding(false));
            string json = JsonUtility.ToJson(new ScoreRowCollection(allRows), true);
            File.WriteAllText(PreviewJsonPath, json, new UTF8Encoding(false));
            AssetDatabase.Refresh();

            if (applyToAssets)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log(
                $"[PawOrderOutcomeEvidenceTool] Outcomes={outcomeGuids.Length} Updated={updated} " +
                $"SkippedExisting={skippedBecauseHasValues} Overwrite={overwriteExisting} " +
                $"CSV={PreviewCsvPath} JSON={PreviewJsonPath}");
        }

        private static ScoreRow BuildRow(InteractionOutcomeData outcome, InteractionContext context, string suspect, string responseLower)
        {
            int motivation = 0;
            int means = 0;
            int opportunity = 0;
            bool mentioned = responseLower.Contains(suspect.ToLowerInvariant());
            bool isTarget = string.Equals(suspect, context.TargetCharacter, StringComparison.OrdinalIgnoreCase);

            if (mentioned)
            {
                motivation += 20;
                means += 20;
                opportunity += 20;
            }

            if (isTarget)
            {
                opportunity += 10;
            }

            if (HasAny(responseLower, "motivo", "inveja", "rival", "conflito", "ressent", "segredo", "encobrir", "vingan", "ciume")) motivation += 20;
            if (HasAny(responseLower, "matar", "assassin", "silenciar", "amea", "chantagem")) motivation += 20;

            if (HasAny(responseLower, "acesso", "entrar", "chave", "rotina", "procedimento", "ajuste", "camarim", "microfone", "batom", "lenco", "lenço")) means += 20;
            if (HasAny(responseLower, "sabotar", "manipul", "raspad", "veneno", "alterado")) means += 20;

            if (HasAny(responseLower, "hora", "antes", "depois", "sozinho", "circulou", "frequentou", "presenca", "presença", "viu", "estava")) opportunity += 20;
            if (HasAny(responseLower, "hora critica", "hora crítica", "duas vezes", "segunda entrada", "janela de tempo")) opportunity += 20;

            string promptText = (context.PromptDisplayName + " " + context.PromptDescription).ToLowerInvariant();
            if (HasAny(promptText, "motivo", "por que")) motivation += 10;
            if (HasAny(promptText, "acesso", "entrar", "rotina", "procedimento", "ajuste"))
            {
                means += 10;
                opportunity += 10;
            }
            if (HasAny(promptText, "onde", "quando", "hora", "sozinho", "frequentou", "circulou")) opportunity += 10;

            if (outcome.ReferencedPrompts.Any())
            {
                string refs = string.Join(" ", outcome.ReferencedPrompts.Where(p => p != null).Select(p => $"{p.DisplayName} {p.Description}")).ToLowerInvariant();
                if (HasAny(refs, "motivo", "rival", "injust", "segredo")) motivation += 10;
                if (HasAny(refs, "acesso", "chave", "rotina", "camarim", "ajuste")) means += 10;
                if (HasAny(refs, "hora", "antes", "depois", "sozinho")) opportunity += 10;
            }

            if (outcome.UnlocksPrompts.Any())
            {
                motivation += 5;
                means += 5;
                opportunity += 5;
            }

            if (!mentioned)
            {
                motivation = Math.Min(motivation, 25);
                means = Math.Min(means, 25);
                opportunity = Math.Min(opportunity, 25);
            }

            motivation = SnapScore(motivation);
            means = SnapScore(means);
            opportunity = SnapScore(opportunity);
            int score = Mathf.RoundToInt((motivation + means + opportunity) / 3f);
            string rationale = BuildRationale(mentioned, isTarget, motivation, means, opportunity);

            return new ScoreRow(
                outcome.OutcomeId,
                context.Question,
                context.TargetCharacter,
                suspect,
                motivation,
                means,
                opportunity,
                score,
                rationale);
        }

        private static Dictionary<string, CharacterData> LoadSuspects()
        {
            Dictionary<string, CharacterData> map = new(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:CharacterData", new[] { "Assets/Project/Scriptables/Investigations/PawOrder/Characters" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (character != null)
                {
                    map[character.name] = character;
                }
            }

            foreach (string suspect in Suspects)
            {
                if (!map.ContainsKey(suspect))
                {
                    throw new InvalidOperationException($"CharacterData nao encontrado para suspeito: {suspect}");
                }
            }

            return map;
        }

        private static Dictionary<string, InteractionContext> LoadInteractionContexts()
        {
            Dictionary<string, InteractionContext> map = new(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:CharacterInteractionData", new[] { InteractionsRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterInteractionData interaction = AssetDatabase.LoadAssetAtPath<CharacterInteractionData>(path);
                if (interaction == null || interaction.Outcome == null || string.IsNullOrWhiteSpace(interaction.Outcome.OutcomeId))
                {
                    continue;
                }

                string question = interaction.PromptUsed != null
                    ? interaction.PromptUsed.DisplayName
                    : ParseOutcomeQuestionFallback(interaction.Outcome.OutcomeId);

                map[interaction.Outcome.OutcomeId] = new InteractionContext(
                    question,
                    interaction.TargetCharacter != null ? interaction.TargetCharacter.name : "Unknown",
                    interaction.PromptUsed != null ? interaction.PromptUsed.DisplayName : string.Empty,
                    interaction.PromptUsed != null ? interaction.PromptUsed.Description : string.Empty);
            }

            return map;
        }

        private static InteractionContext BuildFallbackContext(string outcomeId)
        {
            ParseOutcomeId(outcomeId, out string qid, out string target);
            return new InteractionContext(ParseOutcomeQuestionFallback(outcomeId), target, qid, string.Empty);
        }

        private static string ParseOutcomeQuestionFallback(string outcomeId)
        {
            ParseOutcomeId(outcomeId, out string qid, out _);
            return qid;
        }

        private static void ParseOutcomeId(string outcomeId, out string qid, out string target)
        {
            qid = "Q00";
            target = "Unknown";
            string[] parts = outcomeId.Split('_');
            if (parts.Length > 1) qid = parts[1];
            if (parts.Length > 2) target = parts[parts.Length - 1];
        }

        private static int SnapScore(int value)
        {
            int[] allowed = { 0, 10, 20, 25, 30, 40, 50, 60, 75, 80, 90, 100 };
            return allowed.OrderBy(v => Math.Abs(v - Mathf.Clamp(value, 0, 100))).First();
        }

        private static bool HasAny(string text, params string[] terms)
        {
            return terms.Any(text.Contains);
        }

        private static string BuildRationale(bool mentioned, bool isTarget, int motivation, int means, int opportunity)
        {
            List<string> reasons = new();
            reasons.Add(mentioned ? "citado na resposta" : "sem citacao direta");
            if (isTarget) reasons.Add("suspeito alvo da interacao");
            if (motivation >= 50) reasons.Add("motivacao mais forte");
            if (means >= 50) reasons.Add("meios mais fortes");
            if (opportunity >= 50) reasons.Add("oportunidade mais forte");
            return string.Join("; ", reasons);
        }

        private static string ToCsv(ScoreRow row)
        {
            static string Esc(string s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";
            return string.Join(",",
                Esc(row.OutcomeId),
                Esc(row.Question),
                Esc(row.TargetCharacter),
                Esc(row.Suspect),
                row.MotivationScore,
                row.MeansScore,
                row.OpportunityScore,
                row.Score,
                Esc(row.Rationale));
        }

        [Serializable]
        private sealed class ScoreRowCollection
        {
            public ScoreRowCollection(List<ScoreRow> rows)
            {
                this.rows = rows;
            }

            public List<ScoreRow> rows;
        }

        private readonly struct InteractionContext
        {
            public InteractionContext(string question, string targetCharacter, string promptDisplayName, string promptDescription)
            {
                Question = question;
                TargetCharacter = targetCharacter;
                PromptDisplayName = promptDisplayName ?? string.Empty;
                PromptDescription = promptDescription ?? string.Empty;
            }

            public string Question { get; }
            public string TargetCharacter { get; }
            public string PromptDisplayName { get; }
            public string PromptDescription { get; }
        }

        [Serializable]
        private readonly struct ScoreRow
        {
            public ScoreRow(string outcomeId, string question, string targetCharacter, string suspect, int motivationScore, int meansScore, int opportunityScore, int score, string rationale)
            {
                OutcomeId = outcomeId;
                Question = question;
                TargetCharacter = targetCharacter;
                Suspect = suspect;
                MotivationScore = motivationScore;
                MeansScore = meansScore;
                OpportunityScore = opportunityScore;
                Score = score;
                Rationale = rationale;
            }

            public string OutcomeId { get; }
            public string Question { get; }
            public string TargetCharacter { get; }
            public string Suspect { get; }
            public int MotivationScore { get; }
            public int MeansScore { get; }
            public int OpportunityScore { get; }
            public int Score { get; }
            public string Rationale { get; }
        }
    }
}
