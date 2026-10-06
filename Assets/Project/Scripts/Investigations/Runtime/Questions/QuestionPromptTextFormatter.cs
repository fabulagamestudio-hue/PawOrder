using System;

namespace Fabula.PawOrder
{
    public static class QuestionPromptTextFormatter
    {
        #region Constants

        private const string SelfPronoun = "você";

        #endregion

        #region Public API

        public static string GetMenuLabel(QuestionPromptData prompt, CharacterData currentNpc)
        {
            if (prompt == null)
            {
                return string.Empty;
            }

            string baseLabel = !string.IsNullOrWhiteSpace(prompt.MenuLabel)
                ? prompt.MenuLabel
                : prompt.DisplayName;

            return ApplySelfPronoun(baseLabel, prompt, currentNpc);
        }

        public static string GetTargetLabel(string targetId, QuestionPromptMenuTargetType targetType, CharacterData currentNpc)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return "Unassigned";
            }

            if (targetType == QuestionPromptMenuTargetType.Character
                && currentNpc != null
                && IsCharacterTargetMatch(targetId, currentNpc.CharacterId))
            {
                return SelfPronoun;
            }

            int separatorIndex = targetId.LastIndexOf('.');
            string rawLabel = separatorIndex >= 0 && separatorIndex < targetId.Length - 1
                ? targetId.Substring(separatorIndex + 1)
                : targetId;

            return NicifyLabel(rawLabel);
        }

        public static bool ShouldUseSelfPronoun(QuestionPromptData prompt, CharacterData currentNpc)
        {
            if (prompt == null || currentNpc == null)
            {
                return false;
            }

            return prompt.MenuTargetType == QuestionPromptMenuTargetType.Character
                && prompt.UseSelfPronounWhenTargetMatchesNpc
                && IsCharacterTargetMatch(prompt.MenuTargetId, currentNpc.CharacterId);
        }

        #endregion

        #region Internal Logic

        private static string ApplySelfPronoun(string baseLabel, QuestionPromptData prompt, CharacterData currentNpc)
        {
            if (string.IsNullOrWhiteSpace(baseLabel) || !ShouldUseSelfPronoun(prompt, currentNpc))
            {
                return baseLabel;
            }

            string characterName = currentNpc.DisplayName;
            if (string.IsNullOrWhiteSpace(characterName))
            {
                characterName = currentNpc.CharacterId;
            }

            if (string.IsNullOrWhiteSpace(characterName))
            {
                return baseLabel;
            }

            return baseLabel.Replace(characterName, SelfPronoun, StringComparison.OrdinalIgnoreCase);
        }

        private static string NicifyLabel(string rawLabel)
        {
            if (string.IsNullOrWhiteSpace(rawLabel))
            {
                return string.Empty;
            }

            string label = rawLabel.Replace("_", " ");
            for (int index = label.Length - 1; index > 0; index--)
            {
                if (char.IsUpper(label[index]) && !char.IsWhiteSpace(label[index - 1]) && !char.IsUpper(label[index - 1]))
                {
                    label = label.Insert(index, " ");
                }
            }

            return label;
        }

        private static bool IsCharacterTargetMatch(string targetId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            if (string.Equals(targetId, characterId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string canonicalCharacterId = "Character." + characterId;
            return string.Equals(targetId, canonicalCharacterId, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
