using System;
using System.Collections.Generic;
using System.Linq;

namespace Fabula.PawOrder
{
    [Serializable]
    public sealed class CaseRuntimeState
    {
        public CaseData CaseData;
        public LocationData CurrentLocation;
        public CharacterData SelectedCulprit;
        public List<LocationData> UnlockedLocations = new();
        public List<ItemPromptData> CollectedItems = new();
        public List<InvestigationPromptData> UnlockedPrompts = new();
        public List<InteractionOutcomeData> DiscoveredOutcomes = new();
        public List<PlannerNodeRuntime> PlannerNodes = new();
        public List<PlannerConnectionRuntime> PlannerConnections = new();

        public void InitializeFrom(CaseData caseData)
        {
            CaseData = caseData;
            CurrentLocation = caseData != null ? caseData.StartingLocation : null;

            UnlockedLocations.Clear();
            CollectedItems.Clear();
            UnlockedPrompts.Clear();
            DiscoveredOutcomes.Clear();
            PlannerNodes.Clear();
            PlannerConnections.Clear();

            if (caseData == null)
            {
                return;
            }

            foreach (LocationData location in caseData.StartingUnlockedLocations)
            {
                AddUnlockedLocation(location);
            }

            foreach (InvestigationPromptData prompt in caseData.StartingPrompts)
            {
                AddUnlockedPrompt(prompt);
            }
        }

        public bool HasPrompt(InvestigationPromptData prompt)
        {
            return prompt != null && UnlockedPrompts.Contains(prompt);
        }

        public bool HasCollectedItem(ItemPromptData item)
        {
            return item != null && CollectedItems.Contains(item);
        }

        public bool HasOutcome(InteractionOutcomeData outcome)
        {
            return outcome != null && DiscoveredOutcomes.Contains(outcome);
        }

        public bool HasUnlockedLocation(LocationData location)
        {
            return location != null && UnlockedLocations.Contains(location);
        }

        public void AddUnlockedPrompt(InvestigationPromptData prompt)
        {
            if (prompt != null && !UnlockedPrompts.Contains(prompt))
            {
                UnlockedPrompts.Add(prompt);
            }
        }

        public void AddCollectedItem(ItemPromptData item)
        {
            if (item != null && !CollectedItems.Contains(item))
            {
                CollectedItems.Add(item);
                AddUnlockedPrompt(item);
            }
        }

        public void AddUnlockedLocation(LocationData location)
        {
            if (location != null && !UnlockedLocations.Contains(location))
            {
                UnlockedLocations.Add(location);
            }
        }

        public void AddDiscoveredOutcome(InteractionOutcomeData outcome)
        {
            if (outcome == null || DiscoveredOutcomes.Contains(outcome))
            {
                return;
            }

            DiscoveredOutcomes.Add(outcome);

            foreach (InvestigationPromptData prompt in outcome.UnlocksPrompts)
            {
                AddUnlockedPrompt(prompt);
            }

            foreach (LocationData location in outcome.UnlocksLocations)
            {
                AddUnlockedLocation(location);
            }
        }

        public bool MeetsRequirements(CharacterInteractionData interaction)
        {
            if (interaction == null)
            {
                return false;
            }

            InteractionRequirements requirements = interaction.Requirements;

            if (requirements == null)
            {
                return true;
            }

            bool hasPrompts = requirements.RequiredKnownPrompts.All(HasPrompt);
            bool hasOutcomes = requirements.RequiredKnownOutcomes.All(HasOutcome);
            bool hasItems = requirements.RequiredCollectedItems.All(HasCollectedItem);
            bool hasLocations = requirements.RequiredUnlockedLocations.All(HasUnlockedLocation);

            return hasPrompts && hasOutcomes && hasItems && hasLocations;
        }
    }
}
