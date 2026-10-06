using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fabula.PawOrder
{
    [CreateAssetMenu(
        fileName = "CharacterInteractionData",
        menuName = "Fabula/Paw Order/Investigations/Character Interaction")]
    public sealed class CharacterInteractionData : ScriptableObject
    {
        [SerializeField] private string interactionId;
        [SerializeField] private CharacterData targetCharacter;
        [SerializeField] private InvestigationPromptData promptUsed;
        [SerializeField] private InteractionOutcomeData outcome;
        [SerializeField] private InteractionRequirements requirements = new();
        [SerializeField] private bool canRepeat;
        [SerializeField] private bool consumeInteraction;

        public string InteractionId => interactionId;
        public CharacterData TargetCharacter => targetCharacter;
        public InvestigationPromptData PromptUsed => promptUsed;
        public InteractionOutcomeData Outcome => outcome;
        public InteractionRequirements Requirements => requirements;
        public bool CanRepeat => canRepeat;
        public bool ConsumeInteraction => consumeInteraction;
    }

    [Serializable]
    public sealed class InteractionRequirements
    {
        [SerializeField] private List<InvestigationPromptData> requiredKnownPrompts = new();
        [SerializeField] private List<InteractionOutcomeData> requiredKnownOutcomes = new();
        [SerializeField] private List<ItemPromptData> requiredCollectedItems = new();
        [SerializeField] private List<LocationData> requiredUnlockedLocations = new();

        public IReadOnlyList<InvestigationPromptData> RequiredKnownPrompts => requiredKnownPrompts;
        public IReadOnlyList<InteractionOutcomeData> RequiredKnownOutcomes => requiredKnownOutcomes;
        public IReadOnlyList<ItemPromptData> RequiredCollectedItems => requiredCollectedItems;
        public IReadOnlyList<LocationData> RequiredUnlockedLocations => requiredUnlockedLocations;
    }
}
