using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    internal static class PawOrderEvidenceAuthoringUtility
    {
        #region Fields

        internal const string DefaultAssetSearchFolder = "Assets/Project/Scriptables/Investigations/PawOrder";
        internal const string EvidenceValuesPropertyName = "suspectEvidenceValues";
        internal const string SuspectPropertyName = "suspect";
        internal const string ScorePropertyName = "score";
        internal const string MotivationScorePropertyName = "motivationScore";
        internal const string MeansScorePropertyName = "meansScore";
        internal const string OpportunityScorePropertyName = "opportunityScore";

        #endregion

        #region Public API

        internal static List<CharacterData> LoadSuspects(string assetSearchFolder)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterData", new[] { assetSearchFolder });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterData>)
                .Where(character => character != null && character.IsSuspect)
                .OrderBy(character => GetCharacterLabel(character), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static List<CharacterInteractionData> LoadInteractions(string assetSearchFolder)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterInteractionData", new[] { assetSearchFolder });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterInteractionData>)
                .Where(interaction => interaction != null && interaction.Outcome != null)
                .OrderBy(GetInteractionSortKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static List<InteractionOutcomeData> LoadOutcomes(string assetSearchFolder)
        {
            string[] guids = AssetDatabase.FindAssets("t:InteractionOutcomeData", new[] { assetSearchFolder });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<InteractionOutcomeData>)
                .Where(outcome => outcome != null)
                .OrderBy(outcome => outcome.OutcomeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static List<PawOrderEvidenceSuggestion> BuildSuggestions(InteractionOutcomeData outcome, IReadOnlyList<CharacterData> suspects)
        {
            return BuildTextSuggestions(outcome, suspects);
        }

        internal static List<PawOrderEvidenceSuggestion> BuildSuggestions(
            InteractionOutcomeData outcome,
            IReadOnlyList<CharacterData> suspects,
            PawOrderEvidenceContextDatabase contextDatabase)
        {
            return PawOrderEvidenceContextSuggestionService.BuildSuggestions(outcome, suspects, contextDatabase);
        }

        internal static List<PawOrderEvidenceSuggestion> BuildTextSuggestions(InteractionOutcomeData outcome, IReadOnlyList<CharacterData> suspects)
        {
            List<PawOrderEvidenceSuggestion> suggestions = new List<PawOrderEvidenceSuggestion>();
            if (outcome == null || suspects == null || suspects.Count == 0)
            {
                return suggestions;
            }

            string responseText = outcome.ResponseText ?? string.Empty;
            foreach (CharacterData suspect in suspects)
            {
                if (suspect == null)
                {
                    continue;
                }

                if (!ContainsCharacterReference(responseText, suspect))
                {
                    continue;
                }

                suggestions.Add(new PawOrderEvidenceSuggestion(
                    suspect,
                    1,
                    0,
                    0,
                    1,
                    "Direct mention: the suspect appears in the answer text, so this answer may increase direct suspicion and opportunity impact if the player believes it."));
            }

            return suggestions;
        }

        internal static void EnsureAllSuspects(InteractionOutcomeData outcome, IReadOnlyList<CharacterData> suspects)
        {
            if (outcome == null || suspects == null || suspects.Count == 0)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(outcome);
            SerializedProperty valuesProperty = serializedObject.FindProperty(EvidenceValuesPropertyName);
            if (valuesProperty == null || !valuesProperty.isArray)
            {
                return;
            }

            foreach (CharacterData suspect in suspects.Where(suspect => suspect != null))
            {
                EnsureEvidenceEntry(valuesProperty, suspect);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(outcome);
        }

        internal static void ClearEvidence(InteractionOutcomeData outcome)
        {
            if (outcome == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(outcome);
            SerializedProperty valuesProperty = serializedObject.FindProperty(EvidenceValuesPropertyName);
            if (valuesProperty == null || !valuesProperty.isArray)
            {
                return;
            }

            valuesProperty.arraySize = 0;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(outcome);
        }

        internal static void ApplySuggestions(InteractionOutcomeData outcome, IReadOnlyList<PawOrderEvidenceSuggestion> suggestions)
        {
            if (outcome == null || suggestions == null || suggestions.Count == 0)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(outcome);
            SerializedProperty valuesProperty = serializedObject.FindProperty(EvidenceValuesPropertyName);
            if (valuesProperty == null || !valuesProperty.isArray)
            {
                return;
            }

            foreach (PawOrderEvidenceSuggestion suggestion in suggestions)
            {
                if (suggestion.Suspect == null)
                {
                    continue;
                }

                SerializedProperty entryProperty = EnsureEvidenceEntry(valuesProperty, suggestion.Suspect);
                ApplyMinimumScore(entryProperty, ScorePropertyName, suggestion.Score);
                ApplyMinimumScore(entryProperty, MotivationScorePropertyName, suggestion.MotivationScore);
                ApplyMinimumScore(entryProperty, MeansScorePropertyName, suggestion.MeansScore);
                ApplyMinimumScore(entryProperty, OpportunityScorePropertyName, suggestion.OpportunityScore);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(outcome);
        }

        internal static string GetCharacterLabel(CharacterData character)
        {
            if (character == null)
            {
                return "None";
            }

            if (!string.IsNullOrWhiteSpace(character.DisplayName))
            {
                return character.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(character.CharacterId))
            {
                return character.CharacterId;
            }

            return character.name;
        }

        internal static string GetPromptLabel(InvestigationPromptData prompt)
        {
            if (prompt == null)
            {
                return "No Prompt";
            }

            if (prompt is QuestionPromptData question && !string.IsNullOrWhiteSpace(question.QuestionId))
            {
                return question.QuestionId + " - " + prompt.DisplayName;
            }

            return string.IsNullOrWhiteSpace(prompt.DisplayName) ? prompt.name : prompt.DisplayName;
        }

        internal static int GetTotalImpact(InteractionOutcomeData outcome)
        {
            if (outcome == null || outcome.SuspectEvidenceValues == null)
            {
                return 0;
            }

            return outcome.SuspectEvidenceValues.Sum(value => value != null ? value.Score : 0);
        }

        internal static int GetAxisImpact(InteractionOutcomeData outcome)
        {
            if (outcome == null || outcome.SuspectEvidenceValues == null)
            {
                return 0;
            }

            return outcome.SuspectEvidenceValues.Sum(value => value != null ? value.MotivationScore + value.MeansScore + value.OpportunityScore : 0);
        }

        #endregion

        #region Internal Logic

        private static bool ContainsCharacterReference(string text, CharacterData character)
        {
            if (string.IsNullOrWhiteSpace(text) || character == null)
            {
                return false;
            }

            return ContainsToken(text, character.name)
                || ContainsToken(text, character.DisplayName)
                || ContainsToken(text, character.CharacterId);
        }

        private static bool ContainsToken(string text, string token)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                text,
                token,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        private static SerializedProperty EnsureEvidenceEntry(SerializedProperty valuesProperty, CharacterData suspect)
        {
            SerializedProperty existingProperty = FindEvidenceEntry(valuesProperty, suspect);
            if (existingProperty != null)
            {
                return existingProperty;
            }

            int index = valuesProperty.arraySize;
            valuesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty entryProperty = valuesProperty.GetArrayElementAtIndex(index);
            entryProperty.FindPropertyRelative(SuspectPropertyName).objectReferenceValue = suspect;
            entryProperty.FindPropertyRelative(ScorePropertyName).intValue = 0;
            entryProperty.FindPropertyRelative(MotivationScorePropertyName).intValue = 0;
            entryProperty.FindPropertyRelative(MeansScorePropertyName).intValue = 0;
            entryProperty.FindPropertyRelative(OpportunityScorePropertyName).intValue = 0;
            return entryProperty;
        }

        private static SerializedProperty FindEvidenceEntry(SerializedProperty valuesProperty, CharacterData suspect)
        {
            for (int i = 0; i < valuesProperty.arraySize; i++)
            {
                SerializedProperty entryProperty = valuesProperty.GetArrayElementAtIndex(i);
                SerializedProperty suspectProperty = entryProperty.FindPropertyRelative(SuspectPropertyName);
                if (suspectProperty.objectReferenceValue == suspect)
                {
                    return entryProperty;
                }
            }

            return null;
        }

        private static void ApplyMinimumScore(SerializedProperty entryProperty, string propertyName, int minimumValue)
        {
            SerializedProperty scoreProperty = entryProperty.FindPropertyRelative(propertyName);
            if (scoreProperty == null)
            {
                return;
            }

            scoreProperty.intValue = Mathf.Max(scoreProperty.intValue, minimumValue);
        }

        private static string GetInteractionSortKey(CharacterInteractionData interaction)
        {
            string prompt = interaction.PromptUsed is QuestionPromptData question
                ? question.QuestionId
                : interaction.PromptUsed != null ? interaction.PromptUsed.name : string.Empty;

            string character = interaction.TargetCharacter != null
                ? GetCharacterLabel(interaction.TargetCharacter)
                : string.Empty;

            return prompt + "_" + character + "_" + interaction.name;
        }

        #endregion
    }

    internal readonly struct PawOrderEvidenceSuggestion
    {
        #region Properties

        internal CharacterData Suspect { get; }
        internal int Score { get; }
        internal int MotivationScore { get; }
        internal int MeansScore { get; }
        internal int OpportunityScore { get; }
        internal string Reason { get; }

        #endregion

        #region Public API

        internal PawOrderEvidenceSuggestion(
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
}
