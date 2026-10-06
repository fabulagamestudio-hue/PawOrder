using System;
using System.Collections.Generic;
using System.Linq;

namespace Fabula.PawOrder
{
    public static class QuestionMenuBuilder
    {
        #region Public API

        public static IReadOnlyList<QuestionPromptGroupMenu> Build(
            IEnumerable<QuestionPromptData> prompts,
            CaseRuntimeState runtimeState,
            CharacterData currentNpc,
            IReadOnlyList<CharacterInteractionData> currentNpcInteractions,
            bool includeHidden = false)
        {
            if (prompts == null)
            {
                return Array.Empty<QuestionPromptGroupMenu>();
            }

            List<QuestionPromptMenuEntry> entries = BuildEntries(prompts, runtimeState, currentNpc, currentNpcInteractions, includeHidden);

            return entries
                .GroupBy(entry => entry.Prompt.MenuGroup)
                .OrderBy(group => (int)group.Key)
                .Select(group => BuildGroup(group.Key, group, currentNpc))
                .Where(group => group.Targets.Count > 0)
                .ToList();
        }

        public static string GetGroupLabel(QuestionPromptMenuGroup group)
        {
            switch (group)
            {
                case QuestionPromptMenuGroup.Character:
                    return "Me fale sobre...";
                case QuestionPromptMenuGroup.Location:
                    return "Sobre este lugar...";
                case QuestionPromptMenuGroup.Item:
                    return "Sobre este objeto...";
                case QuestionPromptMenuGroup.Event:
                    return "O que aconteceu...";
                default:
                    return group.ToString();
            }
        }

        #endregion

        #region Internal Logic

        private static List<QuestionPromptMenuEntry> BuildEntries(
            IEnumerable<QuestionPromptData> prompts,
            CaseRuntimeState runtimeState,
            CharacterData currentNpc,
            IReadOnlyList<CharacterInteractionData> currentNpcInteractions,
            bool includeHidden)
        {
            List<QuestionPromptMenuEntry> entries = new List<QuestionPromptMenuEntry>();

            foreach (QuestionPromptData prompt in prompts)
            {
                if (prompt == null)
                {
                    continue;
                }

                QuestionPromptAvailabilityState state = QuestionPromptAvailabilityResolver.Resolve(prompt, runtimeState, currentNpc, currentNpcInteractions);
                if (state == QuestionPromptAvailabilityState.Hidden && !includeHidden)
                {
                    continue;
                }

                entries.Add(new QuestionPromptMenuEntry(
                    prompt,
                    QuestionPromptTextFormatter.GetMenuLabel(prompt, currentNpc),
                    state,
                    prompt.IsFollowUpQuestion && runtimeState != null && runtimeState.HasPrompt(prompt)));
            }

            return entries;
        }

        private static QuestionPromptGroupMenu BuildGroup(
            QuestionPromptMenuGroup group,
            IEnumerable<QuestionPromptMenuEntry> entries,
            CharacterData currentNpc)
        {
            List<QuestionPromptTargetMenu> targets = entries
                .GroupBy(entry => GetTargetKey(entry.Prompt))
                .OrderBy(targetGroup => targetGroup.Min(entry => entry.Prompt.SortOrder))
                .ThenBy(targetGroup => targetGroup.Key, StringComparer.OrdinalIgnoreCase)
                .Select(targetGroup => BuildTarget(targetGroup, currentNpc))
                .ToList();

            return new QuestionPromptGroupMenu(group, GetGroupLabel(group), targets);
        }

        private static QuestionPromptTargetMenu BuildTarget(
            IGrouping<string, QuestionPromptMenuEntry> targetGroup,
            CharacterData currentNpc)
        {
            QuestionPromptData firstPrompt = targetGroup.First().Prompt;
            List<QuestionPromptMenuEntry> sortedEntries = targetGroup
                .OrderBy(entry => entry.Prompt.SortOrder)
                .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string targetLabel = QuestionPromptTextFormatter.GetTargetLabel(
                firstPrompt.MenuTargetId,
                firstPrompt.MenuTargetType,
                currentNpc);

            return new QuestionPromptTargetMenu(targetGroup.Key, targetLabel, sortedEntries);
        }

        private static string GetTargetKey(QuestionPromptData prompt)
        {
            if (prompt == null || string.IsNullOrWhiteSpace(prompt.MenuTargetId))
            {
                return "Unassigned";
            }

            return prompt.MenuTargetId;
        }

        #endregion
    }
}
