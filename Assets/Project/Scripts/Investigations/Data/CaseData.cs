using System.Collections.Generic;
using UnityEngine;

namespace Fabula.PawOrder
{
    [CreateAssetMenu(
        fileName = "CaseData",
        menuName = "Fabula/Paw Order/Investigations/Case Data")]
    public sealed class CaseData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string caseId;
        [SerializeField] private string caseTitle;

        [Header("Start State")]
        [SerializeField] private LocationData startingLocation;
        [SerializeField] private List<LocationData> startingUnlockedLocations = new();
        [SerializeField] private List<InvestigationPromptData> startingPrompts = new();

        [Header("Suspects")]
        [SerializeField] private List<CharacterData> suspects = new();

        [Header("Content Indexes")]
        [SerializeField] private List<LocationData> allLocations = new();
        [SerializeField] private List<CharacterData> allCharacters = new();
        [SerializeField] private List<InvestigationPromptData> allPrompts = new();

        public string CaseId => caseId;
        public string CaseTitle => caseTitle;
        public LocationData StartingLocation => startingLocation;
        public IReadOnlyList<LocationData> StartingUnlockedLocations => startingUnlockedLocations;
        public IReadOnlyList<InvestigationPromptData> StartingPrompts => startingPrompts;
        public IReadOnlyList<CharacterData> Suspects => suspects;
        public IReadOnlyList<LocationData> AllLocations => allLocations;
        public IReadOnlyList<CharacterData> AllCharacters => allCharacters;
        public IReadOnlyList<InvestigationPromptData> AllPrompts => allPrompts;
    }
}
