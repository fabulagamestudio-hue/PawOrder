using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    internal static class PawOrderEvidenceBatchSuggestionApplier
    {
        #region Fields

        private const int MaxAxisScore = 3;

        #endregion

        #region Public API

        internal static List<PawOrderEvidenceBatchSuggestionResult> BuildPreview(
            IReadOnlyList<PawOrderEvidenceAuthoringRow> rows,
            IReadOnlyList<CharacterData> suspects,
            PawOrderEvidenceContextDatabase contextDatabase)
        {
            List<PawOrderEvidenceBatchSuggestionResult> results = new List<PawOrderEvidenceBatchSuggestionResult>();
            if (rows == null || rows.Count == 0)
            {
                return results;
            }

            foreach (PawOrderEvidenceAuthoringRow row in rows)
            {
                if (row == null || row.Outcome == null)
                {
                    continue;
                }

                List<PawOrderEvidenceSuggestion> suggestions = PawOrderEvidenceAuthoringUtility.BuildSuggestions(row.Outcome, suspects, contextDatabase);
                if (suggestions.Count == 0)
                {
                    results.Add(PawOrderEvidenceBatchSuggestionResult.Empty(row));
                    continue;
                }

                List<PawOrderEvidenceBatchSuggestionChange> changes = BuildSafeChanges(row.Outcome, suggestions);
                List<string> conflicts = BuildConflicts(row.Outcome, suggestions, changes);
                results.Add(new PawOrderEvidenceBatchSuggestionResult(row, suggestions, changes, conflicts));
            }

            return results
                .OrderByDescending(result => result.HasSafeChanges)
                .ThenByDescending(result => result.HasConflicts)
                .ThenBy(result => result.Row.SortKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static PawOrderEvidenceBatchApplySummary ApplySafeSuggestions(IReadOnlyList<PawOrderEvidenceBatchSuggestionResult> previewResults)
        {
            PawOrderEvidenceBatchApplySummary summary = new PawOrderEvidenceBatchApplySummary();
            if (previewResults == null || previewResults.Count == 0)
            {
                return summary;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (PawOrderEvidenceBatchSuggestionResult result in previewResults)
                {
                    summary.TotalRows++;
                    if (result == null || result.Row == null || result.Row.Outcome == null)
                    {
                        continue;
                    }

                    if (!result.HasSafeChanges)
                    {
                        if (result.HasConflicts)
                        {
                            summary.ConflictRows++;
                        }

                        continue;
                    }

                    int appliedChanges = ApplySafeChanges(result.Row.Outcome, result.SafeChanges);
                    if (appliedChanges <= 0)
                    {
                        continue;
                    }

                    summary.ChangedRows++;
                    summary.AppliedEntries += appliedChanges;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return summary;
        }

        internal static string BuildReport(IReadOnlyList<PawOrderEvidenceBatchSuggestionResult> previewResults)
        {
            List<string> lines = new List<string>();
            lines.Add("Paw & Order Evidence Batch Suggestion Report");
            lines.Add("Generated At: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            lines.Add(string.Empty);

            if (previewResults == null || previewResults.Count == 0)
            {
                lines.Add("No preview data was generated.");
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add("Rows: " + previewResults.Count.ToString(CultureInfo.InvariantCulture));
            lines.Add("Rows With Safe Changes: " + previewResults.Count(result => result.HasSafeChanges).ToString(CultureInfo.InvariantCulture));
            lines.Add("Rows With Conflicts: " + previewResults.Count(result => result.HasConflicts).ToString(CultureInfo.InvariantCulture));
            lines.Add(string.Empty);

            foreach (PawOrderEvidenceBatchSuggestionResult result in previewResults)
            {
                if (result == null || !result.HasRelevantData)
                {
                    continue;
                }

                lines.Add("---");
                lines.Add("Question: " + result.Row.QuestionLabel);
                lines.Add("Speaker: " + result.Row.SpeakerLabel);
                lines.Add("Outcome: " + (result.Row.Outcome != null ? result.Row.Outcome.name : "None"));
                lines.Add("Safe Changes: " + result.SafeChanges.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("Conflicts: " + result.Conflicts.Count.ToString(CultureInfo.InvariantCulture));

                foreach (PawOrderEvidenceBatchSuggestionChange change in result.SafeChanges)
                {
                    lines.Add("Apply: " + PawOrderEvidenceAuthoringUtility.GetCharacterLabel(change.Suspect) +
                        " | Suspicion +" + change.Score.ToString(CultureInfo.InvariantCulture) +
                        " | Motive +" + change.MotivationScore.ToString(CultureInfo.InvariantCulture) +
                        " | Means +" + change.MeansScore.ToString(CultureInfo.InvariantCulture) +
                        " | Opportunity +" + change.OpportunityScore.ToString(CultureInfo.InvariantCulture));
                }

                foreach (string conflict in result.Conflicts)
                {
                    lines.Add("Conflict: " + conflict);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        #endregion

        #region Internal Logic

        private static List<PawOrderEvidenceBatchSuggestionChange> BuildSafeChanges(
            InteractionOutcomeData outcome,
            IReadOnlyList<PawOrderEvidenceSuggestion> suggestions)
        {
            List<PawOrderEvidenceBatchSuggestionChange> changes = new List<PawOrderEvidenceBatchSuggestionChange>();
            foreach (PawOrderEvidenceSuggestion suggestion in suggestions)
            {
                if (suggestion.Suspect == null || IsEmptySuggestion(suggestion))
                {
                    continue;
                }

                SuspectEvidenceValue existingValue = FindEvidenceValue(outcome, suggestion.Suspect);
                if (existingValue != null && HasAnyImpact(existingValue))
                {
                    continue;
                }

                changes.Add(new PawOrderEvidenceBatchSuggestionChange(
                    suggestion.Suspect,
                    ClampScore(suggestion.Score),
                    ClampScore(suggestion.MotivationScore),
                    ClampScore(suggestion.MeansScore),
                    ClampScore(suggestion.OpportunityScore),
                    suggestion.Reason));
            }

            return changes;
        }

        private static List<string> BuildConflicts(
            InteractionOutcomeData outcome,
            IReadOnlyList<PawOrderEvidenceSuggestion> suggestions,
            IReadOnlyList<PawOrderEvidenceBatchSuggestionChange> safeChanges)
        {
            List<string> conflicts = new List<string>();
            foreach (PawOrderEvidenceSuggestion suggestion in suggestions)
            {
                if (suggestion.Suspect == null || IsEmptySuggestion(suggestion))
                {
                    continue;
                }

                if (safeChanges.Any(change => change.Suspect == suggestion.Suspect))
                {
                    continue;
                }

                SuspectEvidenceValue existingValue = FindEvidenceValue(outcome, suggestion.Suspect);
                if (existingValue != null && HasAnyImpact(existingValue))
                {
                    conflicts.Add(PawOrderEvidenceAuthoringUtility.GetCharacterLabel(suggestion.Suspect) + " already has manual evidence impact and was skipped.");
                }
            }

            return conflicts;
        }

        private static int ApplySafeChanges(InteractionOutcomeData outcome, IReadOnlyList<PawOrderEvidenceBatchSuggestionChange> changes)
        {
            if (outcome == null || changes == null || changes.Count == 0)
            {
                return 0;
            }

            SerializedObject serializedObject = new SerializedObject(outcome);
            SerializedProperty valuesProperty = serializedObject.FindProperty(PawOrderEvidenceAuthoringUtility.EvidenceValuesPropertyName);
            if (valuesProperty == null || !valuesProperty.isArray)
            {
                return 0;
            }

            int appliedChanges = 0;
            foreach (PawOrderEvidenceBatchSuggestionChange change in changes)
            {
                if (change.Suspect == null)
                {
                    continue;
                }

                SerializedProperty entryProperty = FindEvidenceEntry(valuesProperty, change.Suspect);
                if (entryProperty != null && HasAnySerializedImpact(entryProperty))
                {
                    continue;
                }

                if (entryProperty == null)
                {
                    entryProperty = CreateEvidenceEntry(valuesProperty, change.Suspect);
                }

                SetScore(entryProperty, PawOrderEvidenceAuthoringUtility.ScorePropertyName, change.Score);
                SetScore(entryProperty, PawOrderEvidenceAuthoringUtility.MotivationScorePropertyName, change.MotivationScore);
                SetScore(entryProperty, PawOrderEvidenceAuthoringUtility.MeansScorePropertyName, change.MeansScore);
                SetScore(entryProperty, PawOrderEvidenceAuthoringUtility.OpportunityScorePropertyName, change.OpportunityScore);
                appliedChanges++;
            }

            if (appliedChanges > 0)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(outcome);
            }

            return appliedChanges;
        }

        private static SuspectEvidenceValue FindEvidenceValue(InteractionOutcomeData outcome, CharacterData suspect)
        {
            if (outcome == null || outcome.SuspectEvidenceValues == null || suspect == null)
            {
                return null;
            }

            foreach (SuspectEvidenceValue value in outcome.SuspectEvidenceValues)
            {
                if (value != null && value.Suspect == suspect)
                {
                    return value;
                }
            }

            return null;
        }

        private static SerializedProperty FindEvidenceEntry(SerializedProperty valuesProperty, CharacterData suspect)
        {
            for (int i = 0; i < valuesProperty.arraySize; i++)
            {
                SerializedProperty entryProperty = valuesProperty.GetArrayElementAtIndex(i);
                SerializedProperty suspectProperty = entryProperty.FindPropertyRelative(PawOrderEvidenceAuthoringUtility.SuspectPropertyName);
                if (suspectProperty.objectReferenceValue == suspect)
                {
                    return entryProperty;
                }
            }

            return null;
        }

        private static SerializedProperty CreateEvidenceEntry(SerializedProperty valuesProperty, CharacterData suspect)
        {
            int index = valuesProperty.arraySize;
            valuesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty entryProperty = valuesProperty.GetArrayElementAtIndex(index);
            entryProperty.FindPropertyRelative(PawOrderEvidenceAuthoringUtility.SuspectPropertyName).objectReferenceValue = suspect;
            SetScore(entryProperty, PawOrderEvidenceAuthoringUtility.ScorePropertyName, 0);
            SetScore(entryProperty, PawOrderEvidenceAuthoringUtility.MotivationScorePropertyName, 0);
            SetScore(entryProperty, PawOrderEvidenceAuthoringUtility.MeansScorePropertyName, 0);
            SetScore(entryProperty, PawOrderEvidenceAuthoringUtility.OpportunityScorePropertyName, 0);
            return entryProperty;
        }

        private static bool HasAnyImpact(SuspectEvidenceValue value)
        {
            return value.Score != 0 || value.MotivationScore != 0 || value.MeansScore != 0 || value.OpportunityScore != 0;
        }

        private static bool HasAnySerializedImpact(SerializedProperty entryProperty)
        {
            return GetScore(entryProperty, PawOrderEvidenceAuthoringUtility.ScorePropertyName) != 0
                || GetScore(entryProperty, PawOrderEvidenceAuthoringUtility.MotivationScorePropertyName) != 0
                || GetScore(entryProperty, PawOrderEvidenceAuthoringUtility.MeansScorePropertyName) != 0
                || GetScore(entryProperty, PawOrderEvidenceAuthoringUtility.OpportunityScorePropertyName) != 0;
        }

        private static bool IsEmptySuggestion(PawOrderEvidenceSuggestion suggestion)
        {
            return suggestion.Score == 0 && suggestion.MotivationScore == 0 && suggestion.MeansScore == 0 && suggestion.OpportunityScore == 0;
        }

        private static int GetScore(SerializedProperty entryProperty, string propertyName)
        {
            SerializedProperty scoreProperty = entryProperty.FindPropertyRelative(propertyName);
            return scoreProperty != null ? scoreProperty.intValue : 0;
        }

        private static void SetScore(SerializedProperty entryProperty, string propertyName, int value)
        {
            SerializedProperty scoreProperty = entryProperty.FindPropertyRelative(propertyName);
            if (scoreProperty == null)
            {
                return;
            }

            scoreProperty.intValue = ClampScore(value);
        }

        private static int ClampScore(int value)
        {
            return Mathf.Clamp(value, 0, MaxAxisScore);
        }

        #endregion
    }

    internal sealed class PawOrderEvidenceBatchSuggestionResult
    {
        #region Properties

        internal PawOrderEvidenceAuthoringRow Row { get; }
        internal IReadOnlyList<PawOrderEvidenceSuggestion> Suggestions { get; }
        internal IReadOnlyList<PawOrderEvidenceBatchSuggestionChange> SafeChanges { get; }
        internal IReadOnlyList<string> Conflicts { get; }
        internal bool HasSafeChanges => SafeChanges.Count > 0;
        internal bool HasConflicts => Conflicts.Count > 0;
        internal bool HasRelevantData => Suggestions.Count > 0 || SafeChanges.Count > 0 || Conflicts.Count > 0;

        #endregion

        #region Constructors

        internal PawOrderEvidenceBatchSuggestionResult(
            PawOrderEvidenceAuthoringRow row,
            IReadOnlyList<PawOrderEvidenceSuggestion> suggestions,
            IReadOnlyList<PawOrderEvidenceBatchSuggestionChange> safeChanges,
            IReadOnlyList<string> conflicts)
        {
            Row = row;
            Suggestions = suggestions ?? Array.Empty<PawOrderEvidenceSuggestion>();
            SafeChanges = safeChanges ?? Array.Empty<PawOrderEvidenceBatchSuggestionChange>();
            Conflicts = conflicts ?? Array.Empty<string>();
        }

        #endregion

        #region Public API

        internal static PawOrderEvidenceBatchSuggestionResult Empty(PawOrderEvidenceAuthoringRow row)
        {
            return new PawOrderEvidenceBatchSuggestionResult(
                row,
                Array.Empty<PawOrderEvidenceSuggestion>(),
                Array.Empty<PawOrderEvidenceBatchSuggestionChange>(),
                Array.Empty<string>());
        }

        #endregion
    }

    internal readonly struct PawOrderEvidenceBatchSuggestionChange
    {
        #region Properties

        internal CharacterData Suspect { get; }
        internal int Score { get; }
        internal int MotivationScore { get; }
        internal int MeansScore { get; }
        internal int OpportunityScore { get; }
        internal string Reason { get; }

        #endregion

        #region Constructors

        internal PawOrderEvidenceBatchSuggestionChange(
            CharacterData suspect,
            int score,
            int motivationScore,
            int meansScore,
            int opportunityScore,
            string reason)
        {
            Suspect = suspect;
            Score = score;
            MotivationScore = motivationScore;
            MeansScore = meansScore;
            OpportunityScore = opportunityScore;
            Reason = reason;
        }

        #endregion
    }

    internal sealed class PawOrderEvidenceBatchApplySummary
    {
        #region Properties

        internal int TotalRows { get; set; }
        internal int ChangedRows { get; set; }
        internal int AppliedEntries { get; set; }
        internal int ConflictRows { get; set; }

        #endregion
    }
}
