using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    internal sealed class PawOrderEvidenceImpactValidatorService
    {
        #region Fields

        private const int StrongScoreThreshold = 61;
        private const int CentralScoreThreshold = 86;
        private const int LowScoreThreshold = 15;
        private const string MissingValue = "(missing)";

        #endregion

        #region Public API

        public PawOrderEvidenceImpactReport Validate(string truthWorkbookPath, string questionnaireWorkbookPath, string assetSearchFolder)
        {
            PawOrderEvidenceImpactReport report = new PawOrderEvidenceImpactReport();
            PawOrderTruthDatabase truthDatabase = LoadTruthDatabase(truthWorkbookPath, report);
            PawOrderQuestionnaireDatabase questionnaireDatabase = LoadQuestionnaireDatabase(questionnaireWorkbookPath, report);

            List<CharacterInteractionData> interactions = LoadAssets<CharacterInteractionData>(assetSearchFolder);
            List<CaseData> cases = LoadAssets<CaseData>(assetSearchFolder);
            List<CharacterData> suspects = ResolveSuspects(cases, assetSearchFolder);
            Dictionary<string, CharacterInteractionData> interactionsByQuestionAndNpc = BuildInteractionIndex(interactions, report);

            ValidateQuestionnaireCoverage(questionnaireDatabase, interactionsByQuestionAndNpc, report);
            ValidateInteractions(interactions, questionnaireDatabase, truthDatabase, suspects, report);
            ValidateBalance(report, truthDatabase);
            return report;
        }

        #endregion

        #region Internal Logic

        private static PawOrderTruthDatabase LoadTruthDatabase(string truthWorkbookPath, PawOrderEvidenceImpactReport report)
        {
            if (string.IsNullOrWhiteSpace(truthWorkbookPath) || !File.Exists(truthWorkbookPath))
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Truth Workbook", "Truth workbook file was not found: " + truthWorkbookPath);
                return new PawOrderTruthDatabase();
            }

            try
            {
                PawOrderExcelWorkbookReader workbook = PawOrderExcelWorkbookReader.Load(truthWorkbookPath);
                return PawOrderTruthDatabase.FromWorkbook(workbook);
            }
            catch (Exception exception)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Truth Workbook", "Failed to import truth workbook: " + exception.Message);
                return new PawOrderTruthDatabase();
            }
        }

        private static PawOrderQuestionnaireDatabase LoadQuestionnaireDatabase(string questionnaireWorkbookPath, PawOrderEvidenceImpactReport report)
        {
            if (string.IsNullOrWhiteSpace(questionnaireWorkbookPath) || !File.Exists(questionnaireWorkbookPath))
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Question Workbook", "Question workbook file was not found: " + questionnaireWorkbookPath);
                return new PawOrderQuestionnaireDatabase();
            }

            try
            {
                PawOrderExcelWorkbookReader workbook = PawOrderExcelWorkbookReader.Load(questionnaireWorkbookPath);
                return PawOrderQuestionnaireDatabase.FromWorkbook(workbook);
            }
            catch (Exception exception)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Question Workbook", "Failed to import question workbook: " + exception.Message);
                return new PawOrderQuestionnaireDatabase();
            }
        }

        private static List<T> LoadAssets<T>(string assetSearchFolder) where T : UnityEngine.Object
        {
            string[] searchFolders = string.IsNullOrWhiteSpace(assetSearchFolder) ? null : new[] { assetSearchFolder };
            string[] guids = searchFolders == null
                ? AssetDatabase.FindAssets("t:" + typeof(T).Name)
                : AssetDatabase.FindAssets("t:" + typeof(T).Name, searchFolders);

            List<T> assets = new List<T>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        private static List<CharacterData> ResolveSuspects(IReadOnlyList<CaseData> cases, string assetSearchFolder)
        {
            foreach (CaseData caseData in cases)
            {
                if (caseData != null && caseData.Suspects != null && caseData.Suspects.Count > 0)
                {
                    return caseData.Suspects.Where(suspect => suspect != null).Distinct().ToList();
                }
            }

            return LoadAssets<CharacterData>(assetSearchFolder)
                .Where(character => character != null && (character.IsSuspect || character.CanBeChosenAsCulprit))
                .Distinct()
                .ToList();
        }

        private static Dictionary<string, CharacterInteractionData> BuildInteractionIndex(IEnumerable<CharacterInteractionData> interactions, PawOrderEvidenceImpactReport report)
        {
            Dictionary<string, CharacterInteractionData> index = new Dictionary<string, CharacterInteractionData>(StringComparer.OrdinalIgnoreCase);
            foreach (CharacterInteractionData interaction in interactions)
            {
                if (interaction == null)
                {
                    continue;
                }

                QuestionPromptData question = interaction.PromptUsed as QuestionPromptData;
                if (question == null || interaction.TargetCharacter == null)
                {
                    continue;
                }

                string key = BuildQuestionNpcKey(question.QuestionId, interaction.TargetCharacter.DisplayName);
                if (index.ContainsKey(key))
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Duplicate Interaction", "More than one interaction was found for question " + question.QuestionId + " and NPC " + interaction.TargetCharacter.DisplayName + ".", interaction);
                    continue;
                }

                index[key] = interaction;
            }

            return index;
        }

        private static void ValidateQuestionnaireCoverage(PawOrderQuestionnaireDatabase questionnaireDatabase, IReadOnlyDictionary<string, CharacterInteractionData> interactionsByQuestionAndNpc, PawOrderEvidenceImpactReport report)
        {
            if (questionnaireDatabase.Questions.Count == 0)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Question Workbook", "No questions were imported from the question workbook.");
                return;
            }

            foreach (PawOrderQuestionnaireQuestion question in questionnaireDatabase.Questions)
            {
                foreach (KeyValuePair<string, string> responseByNpc in question.ResponsesByNpc)
                {
                    string key = BuildQuestionNpcKey(question.QuestionId, responseByNpc.Key);
                    if (!interactionsByQuestionAndNpc.ContainsKey(key))
                    {
                        report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Missing Interaction", "Question " + question.QuestionId + " has a spreadsheet response for NPC " + responseByNpc.Key + ", but no matching CharacterInteractionData was found.");
                    }
                }
            }
        }

        private static void ValidateInteractions(
            IEnumerable<CharacterInteractionData> interactions,
            PawOrderQuestionnaireDatabase questionnaireDatabase,
            PawOrderTruthDatabase truthDatabase,
            IReadOnlyList<CharacterData> suspects,
            PawOrderEvidenceImpactReport report)
        {
            foreach (CharacterInteractionData interaction in interactions)
            {
                ValidateInteraction(interaction, questionnaireDatabase, truthDatabase, suspects, report);
            }
        }

        private static void ValidateInteraction(
            CharacterInteractionData interaction,
            PawOrderQuestionnaireDatabase questionnaireDatabase,
            PawOrderTruthDatabase truthDatabase,
            IReadOnlyList<CharacterData> suspects,
            PawOrderEvidenceImpactReport report)
        {
            if (interaction == null)
            {
                return;
            }

            QuestionPromptData question = interaction.PromptUsed as QuestionPromptData;
            if (question == null)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Info, "Non Question Interaction", "Interaction does not use a QuestionPromptData: " + GetInteractionLabel(interaction), interaction);
                return;
            }

            if (!questionnaireDatabase.ContainsQuestion(question.QuestionId))
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Question Not In Workbook", "Question exists as asset but was not found in the spreadsheet: " + question.QuestionId, question);
            }

            if (interaction.TargetCharacter == null)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Missing Target", "Interaction has no target character: " + GetInteractionLabel(interaction), interaction);
            }

            if (interaction.Outcome == null)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Missing Outcome", "Interaction has no outcome: " + GetInteractionLabel(interaction), interaction);
                return;
            }

            IReadOnlyList<SuspectEvidenceValue> evidenceValues = interaction.Outcome.SuspectEvidenceValues;
            if (evidenceValues == null || evidenceValues.Count == 0)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Missing Evidence", "Outcome has no suspect evidence values: " + interaction.Outcome.name, interaction.Outcome);
                return;
            }

            ValidateMissingSuspectEntries(interaction, suspects, evidenceValues, report);

            bool hasPositiveImpact = false;
            foreach (SuspectEvidenceValue evidenceValue in evidenceValues)
            {
                if (evidenceValue == null)
                {
                    continue;
                }

                hasPositiveImpact |= HasPositiveImpact(evidenceValue);
                PawOrderEvidenceImpactRow row = BuildRow(interaction, question, evidenceValue);
                report.AddRow(row);
                ValidateEvidenceRow(row, truthDatabase, report);
            }

            if (!hasPositiveImpact)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "No Impact", "Outcome does not incriminate any suspect: " + interaction.Outcome.OutcomeId, interaction.Outcome);
            }
        }

        private static void ValidateMissingSuspectEntries(
            CharacterInteractionData interaction,
            IReadOnlyCollection<CharacterData> suspects,
            IReadOnlyCollection<SuspectEvidenceValue> evidenceValues,
            PawOrderEvidenceImpactReport report)
        {
            if (suspects == null || suspects.Count == 0)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Suspect Index", "No suspects were resolved from CaseData or CharacterData assets.");
                return;
            }

            foreach (CharacterData suspect in suspects)
            {
                bool hasEntry = evidenceValues.Any(value => value != null && value.Suspect == suspect);
                if (!hasEntry)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Missing Suspect Evidence", "Outcome " + interaction.Outcome.OutcomeId + " has no evidence entry for suspect " + suspect.DisplayName + ".", interaction.Outcome);
                }
            }
        }

        private static PawOrderEvidenceImpactRow BuildRow(CharacterInteractionData interaction, QuestionPromptData question, SuspectEvidenceValue evidenceValue)
        {
            CharacterData targetCharacter = interaction.TargetCharacter;
            CharacterData suspectCharacter = evidenceValue.Suspect;
            return new PawOrderEvidenceImpactRow
            {
                Interaction = interaction,
                Outcome = interaction.Outcome,
                Prompt = question,
                TargetCharacter = targetCharacter,
                SuspectCharacter = suspectCharacter,
                InteractionId = string.IsNullOrWhiteSpace(interaction.InteractionId) ? interaction.name : interaction.InteractionId,
                OutcomeId = string.IsNullOrWhiteSpace(interaction.Outcome.OutcomeId) ? interaction.Outcome.name : interaction.Outcome.OutcomeId,
                QuestionId = question.QuestionId,
                QuestionText = question.DisplayName,
                TargetNpcName = targetCharacter != null ? targetCharacter.DisplayName : MissingValue,
                SuspectName = suspectCharacter != null ? suspectCharacter.DisplayName : MissingValue,
                MotivationScore = evidenceValue.MotivationScore,
                MeansScore = evidenceValue.MeansScore,
                OpportunityScore = evidenceValue.OpportunityScore,
                Score = evidenceValue.Score,
                ResponseText = interaction.Outcome.ResponseText
            };
        }

        private static void ValidateEvidenceRow(PawOrderEvidenceImpactRow row, PawOrderTruthDatabase truthDatabase, PawOrderEvidenceImpactReport report)
        {
            if (row.SuspectCharacter == null)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Missing Suspect Reference", "Evidence entry has no suspect reference in outcome " + row.OutcomeId + ".", row.Outcome);
                return;
            }

            if (!truthDatabase.TryFindProfile(row.SuspectCharacter, out PawOrderTruthCharacterProfile suspectProfile))
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Truth Profile", "Suspect " + row.SuspectName + " has evidence impact but was not found in the truth workbook.", row.SuspectCharacter);
            }
            else
            {
                ValidateNarrativeSupport(row, suspectProfile, report);
            }

            if (row.Score >= StrongScoreThreshold && row.MotivationScore == 0 && row.MeansScore == 0 && row.OpportunityScore == 0)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "High General Score Without Axis", "Outcome " + row.OutcomeId + " gives a strong general score to " + row.SuspectName + " without any axis score.", row.Outcome);
            }

            if (row.Score == 0 && (row.MotivationScore >= StrongScoreThreshold || row.MeansScore >= StrongScoreThreshold || row.OpportunityScore >= StrongScoreThreshold))
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Axis Score Without General Score", "Outcome " + row.OutcomeId + " strongly incriminates " + row.SuspectName + " on an axis but keeps general score at zero.", row.Outcome);
            }

            if (row.Score >= CentralScoreThreshold && Math.Max(row.MotivationScore, Math.Max(row.MeansScore, row.OpportunityScore)) < StrongScoreThreshold)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Central Score Without Central Axis", "Outcome " + row.OutcomeId + " has central general impact for " + row.SuspectName + " but no strong axis explains it.", row.Outcome);
            }
        }

        private static void ValidateNarrativeSupport(PawOrderEvidenceImpactRow row, PawOrderTruthCharacterProfile suspectProfile, PawOrderEvidenceImpactReport report)
        {
            if (row.MotivationScore >= StrongScoreThreshold && !suspectProfile.HasMotivationSupport)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Narrative Motivation Support", "Strong motivation impact against " + row.SuspectName + " has no clear motivation support in the truth workbook.", row.Outcome);
            }

            if (row.MeansScore >= StrongScoreThreshold && !suspectProfile.HasMeansSupport)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Narrative Means Support", "Strong means impact against " + row.SuspectName + " has no direct item/contact support in the truth workbook.", row.Outcome);
            }

            if (row.OpportunityScore >= StrongScoreThreshold && !suspectProfile.HasOpportunitySupport)
            {
                report.AddIssue(PawOrderEvidenceImpactSeverity.Info, "Narrative Opportunity Support", "Strong opportunity impact against " + row.SuspectName + " is possible as a clue or lie, but the truth workbook does not confirm opportunity support.", row.Outcome);
            }
        }

        private static void ValidateBalance(PawOrderEvidenceImpactReport report, PawOrderTruthDatabase truthDatabase)
        {
            if (report.Rows.Count == 0)
            {
                return;
            }

            ValidateSuspectTotals(report);
            ValidateNpcImpact(report);
            ValidateQuestionSpread(report);
            ValidateCanonicalCulpritTrail(report, truthDatabase);
        }

        private static void ValidateSuspectTotals(PawOrderEvidenceImpactReport report)
        {
            List<IGrouping<string, PawOrderEvidenceImpactRow>> suspectGroups = report.Rows.GroupBy(row => row.SuspectName).ToList();
            if (suspectGroups.Count <= 1)
            {
                return;
            }

            List<float> totals = suspectGroups.Select(group => (float)group.Sum(row => row.Score)).ToList();
            float average = totals.Average();
            float standardDeviation = Mathf.Sqrt(totals.Select(total => (total - average) * (total - average)).Average());

            foreach (IGrouping<string, PawOrderEvidenceImpactRow> suspectGroup in suspectGroups)
            {
                float total = suspectGroup.Sum(row => row.Score);
                if (total > average + standardDeviation)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Info, "High Suspect Total", "Suspect " + suspectGroup.Key + " receives a much higher total impact than average: " + total.ToString("0", CultureInfo.InvariantCulture) + ".");
                }
                else if (total < average - standardDeviation)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Info, "Low Suspect Total", "Suspect " + suspectGroup.Key + " receives a much lower total impact than average: " + total.ToString("0", CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static void ValidateNpcImpact(PawOrderEvidenceImpactReport report)
        {
            foreach (IGrouping<string, PawOrderEvidenceImpactRow> npcGroup in report.Rows.GroupBy(row => row.TargetNpcName))
            {
                int totalScore = npcGroup.Sum(row => row.Score);
                if (totalScore <= 0)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "NPC Without Impact", "NPC " + npcGroup.Key + " never creates evidence impact.");
                    continue;
                }

                int selfScore = npcGroup.Where(row => string.Equals(row.TargetNpcName, row.SuspectName, StringComparison.OrdinalIgnoreCase)).Sum(row => row.Score);
                if (selfScore >= StrongScoreThreshold && selfScore / (float)totalScore >= 0.6f)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Info, "Self Incrimination", "NPC " + npcGroup.Key + " strongly incriminates themselves. This can be intentional, but should be reviewed.");
                }
            }
        }

        private static void ValidateQuestionSpread(PawOrderEvidenceImpactReport report)
        {
            foreach (IGrouping<string, PawOrderEvidenceImpactRow> questionGroup in report.Rows.GroupBy(row => row.QuestionId + "||" + row.QuestionText))
            {
                int strongSuspects = questionGroup.GroupBy(row => row.SuspectName).Count(group => group.Max(row => row.Score) >= StrongScoreThreshold);
                if (strongSuspects >= 3)
                {
                    PawOrderEvidenceImpactRow firstRow = questionGroup.First();
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Broad Question", "Question " + firstRow.QuestionId + " strongly incriminates " + strongSuspects + " suspects. Check whether it is too broad.", firstRow.Prompt);
                }

                bool allRowsAreWeak = questionGroup.All(row => row.Score <= LowScoreThreshold && row.MotivationScore <= LowScoreThreshold && row.MeansScore <= LowScoreThreshold && row.OpportunityScore <= LowScoreThreshold);
                if (allRowsAreWeak)
                {
                    PawOrderEvidenceImpactRow firstRow = questionGroup.First();
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Weak Question", "Question " + firstRow.QuestionId + " never creates more than weak impact.", firstRow.Prompt);
                }
            }
        }

        private static void ValidateCanonicalCulpritTrail(PawOrderEvidenceImpactReport report, PawOrderTruthDatabase truthDatabase)
        {
            List<PawOrderTruthCharacterProfile> culpritProfiles = truthDatabase.Profiles.Where(profile => profile.IsCanonicalCulprit).ToList();
            foreach (PawOrderTruthCharacterProfile culpritProfile in culpritProfiles)
            {
                List<PawOrderEvidenceImpactRow> culpritRows = report.Rows.Where(row => string.Equals(GetFirstToken(row.SuspectName), GetFirstToken(culpritProfile.CharacterName), StringComparison.OrdinalIgnoreCase)).ToList();
                if (culpritRows.Count == 0)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Error, "Culprit Trail", "Canonical culprit " + culpritProfile.CharacterName + " has no evidence impact rows.");
                    continue;
                }

                if (culpritRows.Max(row => row.MotivationScore) < StrongScoreThreshold)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Culprit Trail", "Canonical culprit " + culpritProfile.CharacterName + " has no strong motivation trail.");
                }

                if (culpritRows.Max(row => row.MeansScore) < StrongScoreThreshold)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Culprit Trail", "Canonical culprit " + culpritProfile.CharacterName + " has no strong means trail.");
                }

                if (culpritRows.Max(row => row.OpportunityScore) < StrongScoreThreshold)
                {
                    report.AddIssue(PawOrderEvidenceImpactSeverity.Warning, "Culprit Trail", "Canonical culprit " + culpritProfile.CharacterName + " has no strong opportunity trail.");
                }
            }
        }

        private static bool HasPositiveImpact(SuspectEvidenceValue evidenceValue)
        {
            return evidenceValue.Score > 0 || evidenceValue.MotivationScore > 0 || evidenceValue.MeansScore > 0 || evidenceValue.OpportunityScore > 0;
        }

        private static string BuildQuestionNpcKey(string questionId, string npcName)
        {
            return NormalizeKey(questionId) + "||" + NormalizeKey(npcName);
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string GetInteractionLabel(CharacterInteractionData interaction)
        {
            if (interaction == null)
            {
                return MissingValue;
            }

            return string.IsNullOrWhiteSpace(interaction.InteractionId) ? interaction.name : interaction.InteractionId;
        }

        private static string GetFirstToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string[] parts = value.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? value : parts[0];
        }

        #endregion
    }
}
