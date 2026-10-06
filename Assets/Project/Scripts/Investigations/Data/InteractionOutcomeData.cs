using System.Collections.Generic;
using UnityEngine;

namespace Fabula.PawOrder
{
    [CreateAssetMenu(
        fileName = "InteractionOutcomeData",
        menuName = "Fabula/Paw Order/Investigations/Interaction Outcome")]
    public sealed class InteractionOutcomeData : ScriptableObject
    {
        [SerializeField] private string outcomeId;
        [SerializeField] [TextArea(3, 8)] private string responseText;
        [SerializeField] [TextArea(2, 5)] private string proofSummary;
        [SerializeField] private bool createsUsableProof;
        [SerializeField] private bool marksPromptAsImportant;
        [SerializeField] private List<InvestigationPromptData> unlocksPrompts = new();
        [SerializeField] private List<CharacterInteractionData> unlocksSpecificInteractions = new();
        [SerializeField] private List<LocationData> unlocksLocations = new();
        [SerializeField] private List<InvestigationPromptData> referencedPrompts = new();
        [SerializeField] private List<SuspectEvidenceValue> suspectEvidenceValues = new();

        public string OutcomeId => outcomeId;
        public string ResponseText => responseText;
        public string ProofSummary => proofSummary;
        public bool CreatesUsableProof => createsUsableProof;
        public bool MarksPromptAsImportant => marksPromptAsImportant;
        public IReadOnlyList<InvestigationPromptData> UnlocksPrompts => unlocksPrompts;
        public IReadOnlyList<CharacterInteractionData> UnlocksSpecificInteractions => unlocksSpecificInteractions;
        public IReadOnlyList<LocationData> UnlocksLocations => unlocksLocations;
        public IReadOnlyList<InvestigationPromptData> ReferencedPrompts => referencedPrompts;
        public IReadOnlyList<SuspectEvidenceValue> SuspectEvidenceValues => suspectEvidenceValues;
    }
}
