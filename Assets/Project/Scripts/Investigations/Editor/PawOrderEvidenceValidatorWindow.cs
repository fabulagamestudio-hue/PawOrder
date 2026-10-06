using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    public sealed class PawOrderEvidenceValidatorWindow : EditorWindow
    {
        private const string BaseFolder = "Assets/Project/Scriptables/Investigations/PawOrder/";
        private const string ReportsFolder = "Assets/Project/Scriptables/Investigations/PawOrder/Reports";
        private const string CsvPath = ReportsFolder + "/EvidenceValidationReport.csv";
        private const string MarkdownPath = ReportsFolder + "/EvidenceValidationReport.md";
        private const int StrongScoreThreshold = 61;
        private const int ShortResponseLengthThreshold = 40;

        private readonly List<RowData> rows = new List<RowData>();
        private readonly List<WarningEntry> warnings = new List<WarningEntry>();
        private readonly List<UnityEngine.Object> scannedAssets = new List<UnityEngine.Object>();
        private readonly Dictionary<string, bool> warningFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, ScoreEditEntry> pendingEditsByKey = new Dictionary<string, ScoreEditEntry>();

        private Vector2 scroll;
        private int selectedAssetIndex = -1;
        private int selectedSuspectIndex;
        private bool scanDone;

        [MenuItem("Tools/Fabula/Paw Order/Validate Evidence Balance")]
        public static void OpenWindow()
        {
            PawOrderEvidenceValidatorWindow window = GetWindow<PawOrderEvidenceValidatorWindow>();
            window.titleContent = new GUIContent("Evidence Validator");
            window.minSize = new Vector2(700f, 480f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Paw & Order Evidence Validator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Read-only scan of evidence balance.");
            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Assets", GUILayout.Height(28f)))
                {
                    ScanAssets();
                }

                using (new EditorGUI.DisabledScope(!scanDone))
                {
                    if (GUILayout.Button("Generate CSV Report", GUILayout.Height(28f)))
                    {
                        GenerateCsvReport();
                    }

                    if (GUILayout.Button("Generate Markdown Report", GUILayout.Height(28f)))
                    {
                        GenerateMarkdownReport();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(scannedAssets.Count == 0))
            {
                if (selectedAssetIndex < 0 || selectedAssetIndex >= scannedAssets.Count)
                {
                    selectedAssetIndex = scannedAssets.Count > 0 ? 0 : -1;
                }

                string[] options = scannedAssets.Select(GetObjectLabel).ToArray();
                selectedAssetIndex = EditorGUILayout.Popup("Selected Asset", selectedAssetIndex, options);

                if (GUILayout.Button("Ping Selected Asset"))
                {
                    PingSelectedAsset();
                }
            }

            EditorGUILayout.Space(8f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSummary();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            if (!scanDone)
            {
                EditorGUILayout.HelpBox("Run Scan Assets to build metrics and warnings.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Scanned Rows", rows.Count.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Warnings (raw)", warnings.Count.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.Space(6f);

            DrawQuickStats();
            EditorGUILayout.Space(8f);
            DrawSuspectBreakdown();
            EditorGUILayout.Space(8f);

            if (warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("No warnings found.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Warnings Grouped", EditorStyles.boldLabel);
            foreach (IGrouping<string, WarningEntry> group in warnings.GroupBy(w => w.Category).OrderByDescending(g => g.Count()))
            {
                string header = group.Key + " (" + group.Count() + ")";
                bool expanded = warningFoldouts.ContainsKey(group.Key) && warningFoldouts[group.Key];
                expanded = EditorGUILayout.Foldout(expanded, header, true);
                warningFoldouts[group.Key] = expanded;
                if (!expanded)
                {
                    continue;
                }

                foreach (string sample in group.Select(g => g.Message).Distinct().Take(6))
                {
                    EditorGUILayout.HelpBox(sample, MessageType.Warning);
                }

                int hidden = group.Select(g => g.Message).Distinct().Count() - 6;
                if (hidden > 0)
                {
                    EditorGUILayout.LabelField("... +" + hidden + " warnings similares");
                }
            }
        }

        private void DrawQuickStats()
        {
            if (rows.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Top Suspects by Total Score", EditorStyles.boldLabel);
            foreach (IGrouping<string, RowData> suspect in rows.GroupBy(r => r.SuspectName).OrderByDescending(g => g.Sum(x => x.Score)).Take(6))
            {
                int total = suspect.Sum(x => x.Score);
                float avg = (float)suspect.Average(x => x.Score);
                EditorGUILayout.LabelField("- " + suspect.Key + ": total=" + total + ", media=" + avg.ToString("0.0", CultureInfo.InvariantCulture));
            }
        }

        private void DrawSuspectBreakdown()
        {
            if (rows.Count == 0)
            {
                return;
            }

            List<IGrouping<string, RowData>> suspects = rows
                .GroupBy(r => r.SuspectName)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (suspects.Count == 0)
            {
                return;
            }

            if (selectedSuspectIndex < 0 || selectedSuspectIndex >= suspects.Count)
            {
                selectedSuspectIndex = 0;
            }

            EditorGUILayout.LabelField("Detalhe por Suspeito", EditorStyles.boldLabel);
            string[] suspectOptions = suspects.Select(g => g.Key).ToArray();
            selectedSuspectIndex = EditorGUILayout.Popup("Suspeito", selectedSuspectIndex, suspectOptions);

            IGrouping<string, RowData> selectedGroup = suspects[selectedSuspectIndex];
            int totalScore = selectedGroup.Sum(x => x.Score);
            int totalMotivation = selectedGroup.Sum(x => x.MotivationScore);
            int totalMeans = selectedGroup.Sum(x => x.MeansScore);
            int totalOpportunity = selectedGroup.Sum(x => x.OpportunityScore);
            EditorGUILayout.LabelField(
                "Total",
                "score=" + totalScore + ", motivation=" + totalMotivation + ", means=" + totalMeans + ", opportunity=" + totalOpportunity);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Perguntas e valores que compoem a pontuacao", EditorStyles.miniBoldLabel);

            foreach (RowData row in selectedGroup.OrderByDescending(x => x.Score).ThenBy(x => x.QuestionId, StringComparer.OrdinalIgnoreCase))
            {
                string questionLabel = string.IsNullOrWhiteSpace(row.QuestionId)
                    ? row.QuestionText
                    : row.QuestionId + " - " + row.QuestionText;
                if (string.IsNullOrWhiteSpace(questionLabel))
                {
                    questionLabel = "(sem pergunta)";
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(questionLabel, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("NPC", row.TargetNpc);
                    EditorGUILayout.LabelField("Outcome", row.OutcomeId);
                    DrawEditableScores(row);
                }
            }
        }

        private void DrawEditableScores(RowData row)
        {
            string key = BuildEditKey(row);
            if (!pendingEditsByKey.TryGetValue(key, out ScoreEditEntry edit))
            {
                edit = new ScoreEditEntry
                {
                    Score = row.Score,
                    MotivationScore = row.MotivationScore,
                    MeansScore = row.MeansScore,
                    OpportunityScore = row.OpportunityScore
                };
                pendingEditsByKey[key] = edit;
            }

            EditorGUI.BeginChangeCheck();
            int score = EditorGUILayout.IntField("Score", edit.Score);
            int motivation = EditorGUILayout.IntField("Motivation", edit.MotivationScore);
            int means = EditorGUILayout.IntField("Means", edit.MeansScore);
            int opportunity = EditorGUILayout.IntField("Opportunity", edit.OpportunityScore);
            if (EditorGUI.EndChangeCheck())
            {
                edit.Score = score;
                edit.MotivationScore = motivation;
                edit.MeansScore = means;
                edit.OpportunityScore = opportunity;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Aplicar", GUILayout.Width(90f)))
                {
                    if (ApplyRowEdit(row, edit))
                    {
                        ScanAssets();
                        return;
                    }
                }

                using (new EditorGUI.DisabledScope(
                           edit.Score == row.Score &&
                           edit.MotivationScore == row.MotivationScore &&
                           edit.MeansScore == row.MeansScore &&
                           edit.OpportunityScore == row.OpportunityScore))
                {
                    if (GUILayout.Button("Reverter", GUILayout.Width(90f)))
                    {
                        edit.Score = row.Score;
                        edit.MotivationScore = row.MotivationScore;
                        edit.MeansScore = row.MeansScore;
                        edit.OpportunityScore = row.OpportunityScore;
                    }
                }
            }
        }

        private bool ApplyRowEdit(RowData row, ScoreEditEntry edit)
        {
            if (row == null || row.Outcome == null)
            {
                return false;
            }

            SerializedObject serializedOutcome = new SerializedObject(row.Outcome);
            SerializedProperty evidenceValues = serializedOutcome.FindProperty("suspectEvidenceValues");
            if (evidenceValues == null || !evidenceValues.isArray)
            {
                Debug.LogWarning("[PawOrderEvidenceValidatorWindow] Nao foi possivel editar: campo suspectEvidenceValues nao encontrado em " + row.Outcome.name);
                return false;
            }

            string rowSuspectId = row.SuspectCharacter != null ? row.SuspectCharacter.CharacterId : null;
            for (int i = 0; i < evidenceValues.arraySize; i++)
            {
                SerializedProperty entry = evidenceValues.GetArrayElementAtIndex(i);
                SerializedProperty suspectProperty = entry.FindPropertyRelative("suspect");
                CharacterData suspect = suspectProperty != null ? suspectProperty.objectReferenceValue as CharacterData : null;
                string suspectId = suspect != null ? suspect.CharacterId : null;

                bool sameSuspect =
                    (row.SuspectCharacter != null && ReferenceEquals(suspect, row.SuspectCharacter)) ||
                    (!string.IsNullOrWhiteSpace(rowSuspectId) && string.Equals(rowSuspectId, suspectId, StringComparison.OrdinalIgnoreCase)) ||
                    (row.SuspectCharacter == null && suspect == null && string.Equals(row.SuspectName, "(null)", StringComparison.OrdinalIgnoreCase));

                if (!sameSuspect)
                {
                    continue;
                }

                Undo.RecordObject(row.Outcome, "Adjust Evidence Scores");
                WriteInt(entry, "score", edit.Score);
                WriteInt(entry, "motivationScore", edit.MotivationScore);
                WriteInt(entry, "meansScore", edit.MeansScore);
                WriteInt(entry, "opportunityScore", edit.OpportunityScore);
                serializedOutcome.ApplyModifiedProperties();
                EditorUtility.SetDirty(row.Outcome);
                AssetDatabase.SaveAssets();

                Debug.Log("[PawOrderEvidenceValidatorWindow] Valores atualizados: outcome=" + row.OutcomeId + ", suspect=" + row.SuspectName + ", score=" + edit.Score + ", motivation=" + edit.MotivationScore + ", means=" + edit.MeansScore + ", opportunity=" + edit.OpportunityScore);
                return true;
            }

            Debug.LogWarning("[PawOrderEvidenceValidatorWindow] Nao foi possivel localizar o SuspectEvidenceValue para editar. outcome=" + row.OutcomeId + ", suspect=" + row.SuspectName);
            return false;
        }

        private static void WriteInt(SerializedProperty parent, string fieldName, int value)
        {
            SerializedProperty field = parent.FindPropertyRelative(fieldName);
            if (field != null && field.propertyType == SerializedPropertyType.Integer)
            {
                field.intValue = value;
            }
        }

        private static string BuildEditKey(RowData row)
        {
            string suspectId = row.SuspectCharacter != null ? row.SuspectCharacter.CharacterId : "(null)";
            return row.OutcomeId + "||" + suspectId;
        }

        private void PingSelectedAsset()
        {
            if (selectedAssetIndex < 0 || selectedAssetIndex >= scannedAssets.Count)
            {
                return;
            }

            UnityEngine.Object asset = scannedAssets[selectedAssetIndex];
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        private void ScanAssets()
        {
            rows.Clear();
            warnings.Clear();
            scannedAssets.Clear();
            pendingEditsByKey.Clear();
            selectedSuspectIndex = 0;

            string[] interactionGuids = AssetDatabase.FindAssets("t:CharacterInteractionData", new[] { BaseFolder });
            foreach (string guid in interactionGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterInteractionData interaction = AssetDatabase.LoadAssetAtPath<CharacterInteractionData>(path);
                if (interaction == null)
                {
                    continue;
                }

                scannedAssets.Add(interaction);

                CharacterData targetCharacter = ReadMember<CharacterData>(interaction, "targetCharacter", "TargetCharacter");
                InvestigationPromptData promptUsed = ReadMember<InvestigationPromptData>(interaction, "promptUsed", "PromptUsed");
                InteractionOutcomeData outcome = ReadMember<InteractionOutcomeData>(interaction, "outcome", "Outcome");

                string interactionId = ReadMember<string>(interaction, "interactionId", "InteractionId");
                if (string.IsNullOrWhiteSpace(interactionId))
                {
                    interactionId = interaction.name;
                }

                string questionId = string.Empty;
                string questionText = string.Empty;
                if (promptUsed != null)
                {
                    scannedAssets.Add(promptUsed);
                    questionText = ReadMember<string>(promptUsed, "displayName", "DisplayName");
                    if (string.IsNullOrWhiteSpace(questionText))
                    {
                        questionText = ReadMember<string>(promptUsed, "description", "Description");
                    }

                    QuestionPromptData questionPrompt = promptUsed as QuestionPromptData;
                    questionId = questionPrompt != null
                        ? ReadMember<string>(questionPrompt, "questionId", "QuestionId")
                        : promptUsed.name;
                }

                if (outcome == null)
                {
                    AddWarning("Interaction sem outcome", "Interaction sem outcome: " + interactionId);
                    continue;
                }

                scannedAssets.Add(outcome);
                string outcomeId = ReadMember<string>(outcome, "outcomeId", "OutcomeId");
                string responseText = ReadMember<string>(outcome, "responseText", "ResponseText") ?? string.Empty;
                List<EvidenceEntry> evidenceValues = ReadEvidenceValues(outcome);

                if (evidenceValues == null || evidenceValues.Count == 0)
                {
                    AddWarning("Outcome sem evidence", "Outcome sem suspectEvidenceValues: " + outcome.name + " (" + outcomeId + ")");
                    continue;
                }

                foreach (EvidenceEntry evidence in evidenceValues)
                {
                    rows.Add(new RowData
                    {
                        Interaction = interaction,
                        Outcome = outcome,
                        Prompt = promptUsed,
                        InteractionId = interactionId,
                        OutcomeId = string.IsNullOrWhiteSpace(outcomeId) ? outcome.name : outcomeId,
                        QuestionId = questionId,
                        QuestionText = questionText,
                        TargetNpc = targetCharacter != null ? targetCharacter.DisplayName : "(null)",
                        TargetCharacter = targetCharacter,
                        SuspectName = evidence.Suspect != null ? evidence.Suspect.DisplayName : "(null)",
                        SuspectCharacter = evidence.Suspect,
                        MotivationScore = evidence.MotivationScore,
                        MeansScore = evidence.MeansScore,
                        OpportunityScore = evidence.OpportunityScore,
                        Score = evidence.Score,
                        ResponseText = responseText
                    });
                }
            }

            BuildWarnings();
            scanDone = true;
            Repaint();
            Debug.Log("[PawOrderEvidenceValidatorWindow] Scan completed. Rows=" + rows.Count + ", Warnings=" + warnings.Count);
        }

        private List<EvidenceEntry> ReadEvidenceValues(InteractionOutcomeData outcome)
        {
            IReadOnlyList<SuspectEvidenceValue> values = ReadMember<IReadOnlyList<SuspectEvidenceValue>>(outcome, "suspectEvidenceValues", "SuspectEvidenceValues");
            if (values != null)
            {
                List<EvidenceEntry> typed = new List<EvidenceEntry>();
                foreach (SuspectEvidenceValue value in values)
                {
                    typed.Add(new EvidenceEntry
                    {
                        Suspect = ReadMember<CharacterData>(value, "suspect", "Suspect"),
                        Score = ReadMember(value, "score", "Score", 0),
                        MotivationScore = ReadMember(value, "motivationScore", "MotivationScore", 0),
                        MeansScore = ReadMember(value, "meansScore", "MeansScore", 0),
                        OpportunityScore = ReadMember(value, "opportunityScore", "OpportunityScore", 0)
                    });
                }
                return typed;
            }

            SerializedObject so = new SerializedObject(outcome);
            SerializedProperty property = so.FindProperty("suspectEvidenceValues");
            if (property == null)
            {
                LogMissingField(outcome, "suspectEvidenceValues");
                return null;
            }

            List<EvidenceEntry> list = new List<EvidenceEntry>();
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty entry = property.GetArrayElementAtIndex(i);
                CharacterData suspect = entry.FindPropertyRelative("suspect") != null ? entry.FindPropertyRelative("suspect").objectReferenceValue as CharacterData : null;
                int score = entry.FindPropertyRelative("score") != null ? entry.FindPropertyRelative("score").intValue : 0;
                int motivation = entry.FindPropertyRelative("motivationScore") != null ? entry.FindPropertyRelative("motivationScore").intValue : 0;
                int means = entry.FindPropertyRelative("meansScore") != null ? entry.FindPropertyRelative("meansScore").intValue : 0;
                int opportunity = entry.FindPropertyRelative("opportunityScore") != null ? entry.FindPropertyRelative("opportunityScore").intValue : 0;

                list.Add(new EvidenceEntry
                {
                    Suspect = suspect,
                    Score = score,
                    MotivationScore = motivation,
                    MeansScore = means,
                    OpportunityScore = opportunity
                });
            }

            return list;
        }

        private void BuildWarnings()
        {
            if (rows.Count == 0)
            {
                return;
            }

            Dictionary<string, List<RowData>> rowsBySuspect = rows.GroupBy(r => r.SuspectName).ToDictionary(g => g.Key, g => g.ToList());
            List<float> suspectTotals = rowsBySuspect.Values.Select(v => (float)v.Sum(x => x.Score)).ToList();
            float avg = suspectTotals.Average();
            float std = Mathf.Sqrt(suspectTotals.Select(x => (x - avg) * (x - avg)).Average());

            foreach (KeyValuePair<string, List<RowData>> kvp in rowsBySuspect)
            {
                float total = kvp.Value.Sum(x => x.Score);
                if (total < avg - std)
                {
                    AddWarning("Suspeito abaixo", "Suspeito com score total muito abaixo: " + kvp.Key + " (" + total + ")");
                }
                if (total > avg + std)
                {
                    AddWarning("Suspeito acima", "Suspeito com score total muito acima: " + kvp.Key + " (" + total + ")");
                }

                int maxMot = kvp.Value.Max(x => x.MotivationScore);
                int maxMeans = kvp.Value.Max(x => x.MeansScore);
                int maxOpp = kvp.Value.Max(x => x.OpportunityScore);
                if (maxMot < StrongScoreThreshold) AddWarning("Eixo sem forte", "Suspeito sem evidencia forte em motivation: " + kvp.Key);
                if (maxMeans < StrongScoreThreshold) AddWarning("Eixo sem forte", "Suspeito sem evidencia forte em means: " + kvp.Key);
                if (maxOpp < StrongScoreThreshold) AddWarning("Eixo sem forte", "Suspeito sem evidencia forte em opportunity: " + kvp.Key);
            }

            foreach (IGrouping<string, RowData> npcGroup in rows.GroupBy(r => r.TargetNpc))
            {
                float totalNpc = npcGroup.Sum(x => x.Score);
                float selfScore = npcGroup.Where(x => string.Equals(x.TargetNpc, x.SuspectName, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Score);
                if (totalNpc > 0f && selfScore / totalNpc >= 0.6f && selfScore >= StrongScoreThreshold)
                {
                    AddWarning("Auto incriminacao", "NPC incrimina a si mesmo em excesso: " + npcGroup.Key + " (ratio=" + (selfScore / totalNpc).ToString("P0", CultureInfo.InvariantCulture) + ")");
                }

                if (npcGroup.All(x => x.Score <= 15))
                {
                    AddWarning("NPC sem relevancia", "NPC nunca fornece informacao relevante: " + npcGroup.Key);
                }
            }

            foreach (IGrouping<string, RowData> questionGroup in rows.GroupBy(r => r.QuestionId + "||" + r.QuestionText))
            {
                int suspectsHigh = questionGroup.GroupBy(x => x.SuspectName).Count(g => g.Max(r => r.Score) >= StrongScoreThreshold);
                if (suspectsHigh >= 3)
                {
                    RowData first = questionGroup.First();
                    AddWarning("Pergunta ampla demais", "Pergunta incrimina muitos suspeitos com score alto: " + first.QuestionId + " " + first.QuestionText + " (suspeitos=" + suspectsHigh + ")");
                }
            }

            foreach (RowData row in rows)
            {
                if (row.Score >= StrongScoreThreshold && (row.ResponseText ?? string.Empty).Trim().Length < ShortResponseLengthThreshold)
                {
                    AddWarning("Texto curto x score alto", "Outcome com score alto e responseText muito curto: " + row.OutcomeId + " (" + row.SuspectName + ")");
                }

                if (row.MotivationScore == 0 && row.MeansScore == 0 && row.OpportunityScore == 0)
                {
                    AddWarning("Eixos zerados", "Outcome com motivation/means/opportunity zerados: " + row.OutcomeId + " (" + row.SuspectName + ")");
                }

                int axisAverage = Mathf.RoundToInt((row.MotivationScore + row.MeansScore + row.OpportunityScore) / 3f);
                if (row.Score != axisAverage)
                {
                    AddWarning("Score != media eixos", "Score diferente da media dos eixos: " + row.OutcomeId + " (" + row.SuspectName + ", score=" + row.Score + ", media=" + axisAverage + ")");
                }
            }
        }

        private void GenerateCsvReport()
        {
            if (!scanDone)
            {
                Debug.LogWarning("[PawOrderEvidenceValidatorWindow] Run Scan Assets first.");
                return;
            }

            EnsureReportsFolder();
            List<string> lines = new List<string>();
            lines.Add("interactionId,outcomeId,questionId,questionText,targetNpc,suspect,motivationScore,meansScore,opportunityScore,score,responseText");
            lines.AddRange(rows.Select(ToCsvRow));
            File.WriteAllLines(CsvPath, lines, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("[PawOrderEvidenceValidatorWindow] CSV generated: " + CsvPath);
        }

        private void GenerateMarkdownReport()
        {
            if (!scanDone)
            {
                Debug.LogWarning("[PawOrderEvidenceValidatorWindow] Run Scan Assets first.");
                return;
            }

            EnsureReportsFolder();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Paw & Order Evidence Validation Report");
            sb.AppendLine();
            sb.AppendLine("## Summary by NPC");
            AppendNpcSummary(sb);
            sb.AppendLine();
            sb.AppendLine("## Summary by Suspect");
            AppendSuspectSummary(sb);
            sb.AppendLine();
            sb.AppendLine("## Strongest Evidence");
            AppendStrongestEvidence(sb);
            sb.AppendLine();
            sb.AppendLine("## Weak Evidence");
            AppendWeakEvidence(sb);
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            if (warnings.Count == 0)
            {
                sb.AppendLine("- None.");
            }
            else
            {
                foreach (string warning in warnings.Select(w => w.Message).Distinct())
                {
                    sb.AppendLine("- " + warning);
                }
            }

            File.WriteAllText(MarkdownPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("[PawOrderEvidenceValidatorWindow] Markdown generated: " + MarkdownPath);
        }

        private void AppendNpcSummary(StringBuilder sb)
        {
            foreach (IGrouping<string, RowData> npcGroup in rows.GroupBy(r => r.TargetNpc).OrderBy(g => g.Key))
            {
                sb.AppendLine("### " + npcGroup.Key);
                sb.AppendLine("- respostas analisadas: " + npcGroup.Select(x => x.OutcomeId).Distinct().Count());

                foreach (IGrouping<string, RowData> suspectGroup in npcGroup.GroupBy(x => x.SuspectName).OrderByDescending(g => g.Sum(r => r.Score)))
                {
                    int total = suspectGroup.Sum(x => x.Score);
                    float avg = (float)suspectGroup.Average(x => x.Score);
                    int max = suspectGroup.Max(x => x.Score);
                    int mot = suspectGroup.Sum(x => x.MotivationScore);
                    int means = suspectGroup.Sum(x => x.MeansScore);
                    int opp = suspectGroup.Sum(x => x.OpportunityScore);
                    sb.AppendLine("- suspeito " + suspectGroup.Key + ": total=" + total + ", media=" + avg.ToString("0.0", CultureInfo.InvariantCulture) + ", maior=" + max + ", motivation=" + mot + ", means=" + means + ", opportunity=" + opp);
                }
            }
        }

        private void AppendSuspectSummary(StringBuilder sb)
        {
            foreach (IGrouping<string, RowData> suspectGroup in rows.GroupBy(r => r.SuspectName).OrderBy(g => g.Key))
            {
                int total = suspectGroup.Sum(x => x.Score);
                int mot = suspectGroup.Sum(x => x.MotivationScore);
                int means = suspectGroup.Sum(x => x.MeansScore);
                int opp = suspectGroup.Sum(x => x.OpportunityScore);
                sb.AppendLine("### " + suspectGroup.Key);
                sb.AppendLine("- total acumulado: " + total);
                sb.AppendLine("- total por eixo: motivation=" + mot + ", means=" + means + ", opportunity=" + opp);

                string topNpcs = string.Join(", ", suspectGroup.GroupBy(x => x.TargetNpc).OrderByDescending(g => g.Sum(r => r.Score)).Take(3).Select(g => g.Key + " (" + g.Sum(r => r.Score) + ")"));
                sb.AppendLine("- NPCs que mais incriminam: " + topNpcs);

                string topQuestions = string.Join(", ", suspectGroup.GroupBy(x => x.QuestionId + " " + x.QuestionText).OrderByDescending(g => g.Sum(r => r.Score)).Take(3).Select(g => g.Key + " (" + g.Sum(r => r.Score) + ")"));
                sb.AppendLine("- perguntas que mais incriminam: " + topQuestions);

                string highOutcomes = string.Join(", ", suspectGroup.Where(x => x.Score >= StrongScoreThreshold).OrderByDescending(x => x.Score).Take(6).Select(x => x.OutcomeId + " [" + x.Score + "]"));
                sb.AppendLine("- outcomes com score alto: " + (string.IsNullOrWhiteSpace(highOutcomes) ? "nenhum" : highOutcomes));
            }
        }

        private void AppendStrongestEvidence(StringBuilder sb)
        {
            List<RowData> strongest = rows.Where(x => x.Score >= StrongScoreThreshold).OrderByDescending(x => x.Score).ThenBy(x => x.SuspectName).Take(30).ToList();
            if (strongest.Count == 0)
            {
                sb.AppendLine("- None.");
                return;
            }

            foreach (RowData row in strongest)
            {
                sb.AppendLine("- " + row.Score + " (" + ScoreBand(row.Score) + ") | suspect=" + row.SuspectName + " | npc=" + row.TargetNpc + " | outcome=" + row.OutcomeId + " | question=" + row.QuestionId);
            }
        }

        private void AppendWeakEvidence(StringBuilder sb)
        {
            List<RowData> weakest = rows.Where(x => x.Score <= 15).OrderBy(x => x.Score).ThenBy(x => x.SuspectName).Take(30).ToList();
            if (weakest.Count == 0)
            {
                sb.AppendLine("- None.");
                return;
            }

            foreach (RowData row in weakest)
            {
                sb.AppendLine("- " + row.Score + " (" + ScoreBand(row.Score) + ") | suspect=" + row.SuspectName + " | npc=" + row.TargetNpc + " | outcome=" + row.OutcomeId + " | question=" + row.QuestionId);
            }
        }

        private static string ScoreBand(int score)
        {
            if (score <= 15) return "irrelevante";
            if (score <= 35) return "indicio leve";
            if (score <= 60) return "indicio moderado";
            if (score <= 85) return "indicio forte";
            return "indicio central";
        }

        private static string ToCsvRow(RowData row)
        {
            return string.Join(",",
                Esc(row.InteractionId),
                Esc(row.OutcomeId),
                Esc(row.QuestionId),
                Esc(row.QuestionText),
                Esc(row.TargetNpc),
                Esc(row.SuspectName),
                row.MotivationScore.ToString(CultureInfo.InvariantCulture),
                row.MeansScore.ToString(CultureInfo.InvariantCulture),
                row.OpportunityScore.ToString(CultureInfo.InvariantCulture),
                row.Score.ToString(CultureInfo.InvariantCulture),
                Esc(row.ResponseText));
        }

        private static string Esc(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static void EnsureReportsFolder()
        {
            if (!AssetDatabase.IsValidFolder(ReportsFolder))
            {
                AssetDatabase.CreateFolder("Assets/Project/Scriptables/Investigations/PawOrder", "Reports");
            }
        }

        private static T ReadMember<T>(object source, string privateFieldName, string publicPropertyName)
        {
            T fallback = default(T);
            return ReadMember(source, privateFieldName, publicPropertyName, fallback);
        }

        private static T ReadMember<T>(object source, string privateFieldName, string publicPropertyName, T fallback)
        {
            if (source == null)
            {
                return fallback;
            }

            Type type = source.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo property = type.GetProperty(publicPropertyName, flags);
            if (property != null && property.PropertyType != null)
            {
                object propValue = property.GetValue(source, null);
                if (propValue is T)
                {
                    return (T)propValue;
                }
            }

            FieldInfo field = type.GetField(privateFieldName, flags);
            if (field != null)
            {
                object fieldValue = field.GetValue(source);
                if (fieldValue is T)
                {
                    return (T)fieldValue;
                }
            }

            UnityEngine.Object unityObject = source as UnityEngine.Object;
            if (unityObject != null)
            {
                SerializedObject so = new SerializedObject(unityObject);
                SerializedProperty serializedProperty = so.FindProperty(privateFieldName);
                if (serializedProperty != null)
                {
                    if (typeof(T) == typeof(string) && serializedProperty.propertyType == SerializedPropertyType.String)
                    {
                        return (T)(object)serializedProperty.stringValue;
                    }
                    if (typeof(T) == typeof(int) && serializedProperty.propertyType == SerializedPropertyType.Integer)
                    {
                        return (T)(object)serializedProperty.intValue;
                    }
                    if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)) && serializedProperty.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        return (T)(object)serializedProperty.objectReferenceValue;
                    }
                }
            }

            LogMissingField(source, privateFieldName + " / " + publicPropertyName);
            return fallback;
        }

        private static void LogMissingField(object source, string expectedName)
        {
            UnityEngine.Object unityObject = source as UnityEngine.Object;
            string sourceName = unityObject != null ? unityObject.name : source.GetType().Name;
            Debug.LogWarning("[PawOrderEvidenceValidatorWindow] Campo nao encontrado: " + expectedName + " em " + source.GetType().Name + " (" + sourceName + ")");
        }

        private void AddWarning(string category, string message)
        {
            warnings.Add(new WarningEntry { Category = category, Message = message });
        }

        private sealed class RowData
        {
            public CharacterInteractionData Interaction;
            public InteractionOutcomeData Outcome;
            public InvestigationPromptData Prompt;
            public CharacterData TargetCharacter;
            public CharacterData SuspectCharacter;
            public string InteractionId;
            public string OutcomeId;
            public string QuestionId;
            public string QuestionText;
            public string TargetNpc;
            public string SuspectName;
            public int MotivationScore;
            public int MeansScore;
            public int OpportunityScore;
            public int Score;
            public string ResponseText;
        }

        private sealed class EvidenceEntry
        {
            public CharacterData Suspect;
            public int Score;
            public int MotivationScore;
            public int MeansScore;
            public int OpportunityScore;
        }

        private sealed class WarningEntry
        {
            public string Category;
            public string Message;
        }

        private sealed class ScoreEditEntry
        {
            public int Score;
            public int MotivationScore;
            public int MeansScore;
            public int OpportunityScore;
        }

        private static string GetObjectLabel(UnityEngine.Object obj)
        {
            return obj.GetType().Name + ": " + obj.name;
        }
    }
}
