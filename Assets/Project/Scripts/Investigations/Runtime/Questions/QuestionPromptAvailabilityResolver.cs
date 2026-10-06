using System.Collections.Generic;
using System.Linq;

namespace Fabula.PawOrder
{
    public static class QuestionPromptAvailabilityResolver
    {
        #region Public API

        public static QuestionPromptAvailabilityState Resolve(
            QuestionPromptData prompt,
            CaseRuntimeState runtimeState,
            CharacterData currentNpc,
            IReadOnlyList<CharacterInteractionData> currentNpcInteractions)
        {
            if (prompt == null)
            {
                return QuestionPromptAvailabilityState.Hidden;
            }

            bool isUnlocked = runtimeState != null && runtimeState.HasPrompt(prompt);
            bool isVisible = prompt.StartsVisible || prompt.IsStartingQuestion || isUnlocked;

            if (!isVisible)
            {
                return QuestionPromptAvailabilityState.Hidden;
            }

            if (!isUnlocked)
            {
                return QuestionPromptAvailabilityState.LockedVisible;
            }

            if (currentNpcInteractions == null || currentNpcInteractions.Count == 0)
            {
                return QuestionPromptAvailabilityState.Available;
            }

            bool hasValidInteraction = currentNpcInteractions.Any(interaction => IsInteractionAvailableForPrompt(prompt, runtimeState, currentNpc, interaction));
            return hasValidInteraction
                ? QuestionPromptAvailabilityState.Available
                : QuestionPromptAvailabilityState.LockedVisible;
        }

        #endregion

        #region Internal Logic

        private static bool IsInteractionAvailableForPrompt(
            QuestionPromptData prompt,
            CaseRuntimeState runtimeState,
            CharacterData currentNpc,
            CharacterInteractionData interaction)
        {
            if (interaction == null || interaction.PromptUsed != prompt)
            {
                return false;
            }

            if (currentNpc != null && interaction.TargetCharacter != null && interaction.TargetCharacter != currentNpc)
            {
                return false;
            }

            return runtimeState == null || runtimeState.MeetsRequirements(interaction);
        }

        #endregion
    }
}
