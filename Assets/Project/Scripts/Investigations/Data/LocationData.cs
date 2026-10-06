using System.Collections.Generic;
using UnityEngine;

namespace Fabula.PawOrder
{
    [CreateAssetMenu(
        fileName = "LocationData",
        menuName = "Fabula/Paw Order/Investigations/Location Data")]
    public sealed class LocationData : ScriptableObject
    {
        [SerializeField] private string locationId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite previewImage;
        [SerializeField] private bool availableFromStart;
        [SerializeField] private List<CharacterData> charactersPresent = new();
        [SerializeField] private List<ItemPromptData> collectibleItems = new();
        [SerializeField] private List<LocationData> travelTargets = new();

        public string LocationId => locationId;
        public string DisplayName => displayName;
        public Sprite PreviewImage => previewImage;
        public bool AvailableFromStart => availableFromStart;
        public IReadOnlyList<CharacterData> CharactersPresent => charactersPresent;
        public IReadOnlyList<ItemPromptData> CollectibleItems => collectibleItems;
        public IReadOnlyList<LocationData> TravelTargets => travelTargets;
    }
}
