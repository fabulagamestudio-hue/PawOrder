using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    [CreateAssetMenu(
        fileName = "PawOrderEvidenceContextDatabase",
        menuName = "Fabula/Paw Order/Editor/Evidence Context Database")]
    public sealed class PawOrderEvidenceContextDatabase : ScriptableObject
    {
        #region Fields

        [SerializeField]
        [Tooltip("Characters known by the evidence context importer.")]
        private List<PawOrderEvidenceContextCharacter> characters = new List<PawOrderEvidenceContextCharacter>();

        [SerializeField]
        [Tooltip("Locations known by the evidence context importer.")]
        private List<PawOrderEvidenceContextLocation> locations = new List<PawOrderEvidenceContextLocation>();

        [SerializeField]
        [Tooltip("Items known by the evidence context importer.")]
        private List<PawOrderEvidenceContextItem> items = new List<PawOrderEvidenceContextItem>();

        [SerializeField]
        [Tooltip("Links between characters and locations.")]
        private List<PawOrderEvidenceContextLocationLink> locationLinks = new List<PawOrderEvidenceContextLocationLink>();

        [SerializeField]
        [Tooltip("Links between characters and items.")]
        private List<PawOrderEvidenceContextItemLink> itemLinks = new List<PawOrderEvidenceContextItemLink>();

        #endregion

        #region Properties

        public IReadOnlyList<PawOrderEvidenceContextCharacter> Characters => characters;
        public IReadOnlyList<PawOrderEvidenceContextLocation> Locations => locations;
        public IReadOnlyList<PawOrderEvidenceContextItem> Items => items;
        public IReadOnlyList<PawOrderEvidenceContextLocationLink> LocationLinks => locationLinks;
        public IReadOnlyList<PawOrderEvidenceContextItemLink> ItemLinks => itemLinks;

        #endregion

        #region Public API

        public void ReplaceContent(
            IReadOnlyList<PawOrderEvidenceContextCharacter> importedCharacters,
            IReadOnlyList<PawOrderEvidenceContextLocation> importedLocations,
            IReadOnlyList<PawOrderEvidenceContextItem> importedItems,
            IReadOnlyList<PawOrderEvidenceContextLocationLink> importedLocationLinks,
            IReadOnlyList<PawOrderEvidenceContextItemLink> importedItemLinks)
        {
            characters = importedCharacters != null ? importedCharacters.ToList() : new List<PawOrderEvidenceContextCharacter>();
            locations = importedLocations != null ? importedLocations.ToList() : new List<PawOrderEvidenceContextLocation>();
            items = importedItems != null ? importedItems.ToList() : new List<PawOrderEvidenceContextItem>();
            locationLinks = importedLocationLinks != null ? importedLocationLinks.ToList() : new List<PawOrderEvidenceContextLocationLink>();
            itemLinks = importedItemLinks != null ? importedItemLinks.ToList() : new List<PawOrderEvidenceContextItemLink>();
            EditorUtility.SetDirty(this);
        }

        public IEnumerable<PawOrderEvidenceContextLocationLink> GetLocationLinks(string locationName)
        {
            string key = PawOrderEvidenceContextTextUtility.NormalizeKey(locationName);
            if (string.IsNullOrWhiteSpace(key))
            {
                yield break;
            }

            foreach (PawOrderEvidenceContextLocationLink link in locationLinks)
            {
                if (string.Equals(PawOrderEvidenceContextTextUtility.NormalizeKey(link.LocationName), key, StringComparison.OrdinalIgnoreCase))
                {
                    yield return link;
                }
            }
        }

        public IEnumerable<PawOrderEvidenceContextItemLink> GetItemLinks(string itemName)
        {
            string key = PawOrderEvidenceContextTextUtility.NormalizeKey(itemName);
            if (string.IsNullOrWhiteSpace(key))
            {
                yield break;
            }

            foreach (PawOrderEvidenceContextItemLink link in itemLinks)
            {
                if (string.Equals(PawOrderEvidenceContextTextUtility.NormalizeKey(link.ItemName), key, StringComparison.OrdinalIgnoreCase))
                {
                    yield return link;
                }
            }
        }

        #endregion
    }

    [Serializable]
    public sealed class PawOrderEvidenceContextCharacter
    {
        #region Fields

        [SerializeField]
        [Tooltip("Canonical character name from the mystery spreadsheet.")]
        private string characterName;

        [SerializeField]
        [Tooltip("Alternative tokens that can identify this character in answer text.")]
        private List<string> aliases = new List<string>();

        #endregion

        #region Properties

        public string CharacterName => characterName;
        public IReadOnlyList<string> Aliases => aliases;

        #endregion

        #region Constructors

        public PawOrderEvidenceContextCharacter(string characterName, IEnumerable<string> aliases)
        {
            this.characterName = characterName;
            this.aliases = aliases != null ? aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : new List<string>();
        }

        #endregion
    }

    [Serializable]
    public sealed class PawOrderEvidenceContextLocation
    {
        #region Fields

        [SerializeField]
        [Tooltip("Canonical location name from the mystery spreadsheet.")]
        private string locationName;

        [SerializeField]
        [Tooltip("Alternative tokens that can identify this location in answer text.")]
        private List<string> aliases = new List<string>();

        #endregion

        #region Properties

        public string LocationName => locationName;
        public IReadOnlyList<string> Aliases => aliases;

        #endregion

        #region Constructors

        public PawOrderEvidenceContextLocation(string locationName, IEnumerable<string> aliases)
        {
            this.locationName = locationName;
            this.aliases = aliases != null ? aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : new List<string>();
        }

        #endregion
    }

    [Serializable]
    public sealed class PawOrderEvidenceContextItem
    {
        #region Fields

        [SerializeField]
        [Tooltip("Canonical item name from the mystery spreadsheet.")]
        private string itemName;

        [SerializeField]
        [Tooltip("Alternative tokens that can identify this item in answer text.")]
        private List<string> aliases = new List<string>();

        #endregion

        #region Properties

        public string ItemName => itemName;
        public IReadOnlyList<string> Aliases => aliases;

        #endregion

        #region Constructors

        public PawOrderEvidenceContextItem(string itemName, IEnumerable<string> aliases)
        {
            this.itemName = itemName;
            this.aliases = aliases != null ? aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : new List<string>();
        }

        #endregion
    }

    [Serializable]
    public sealed class PawOrderEvidenceContextLocationLink
    {
        #region Fields

        [SerializeField]
        [Tooltip("Character connected to this location.")]
        private string characterName;

        [SerializeField]
        [Tooltip("Location connected to this character.")]
        private string locationName;

        [SerializeField]
        [Tooltip("Relationship source, such as Timeline or Item Context.")]
        private string relationshipType;

        [SerializeField]
        [Tooltip("Spreadsheet source used to create this link.")]
        private string source;

        #endregion

        #region Properties

        public string CharacterName => characterName;
        public string LocationName => locationName;
        public string RelationshipType => relationshipType;
        public string Source => source;

        #endregion

        #region Constructors

        public PawOrderEvidenceContextLocationLink(string characterName, string locationName, string relationshipType, string source)
        {
            this.characterName = characterName;
            this.locationName = locationName;
            this.relationshipType = relationshipType;
            this.source = source;
        }

        #endregion
    }

    [Serializable]
    public sealed class PawOrderEvidenceContextItemLink
    {
        #region Fields

        [SerializeField]
        [Tooltip("Character connected to this item.")]
        private string characterName;

        [SerializeField]
        [Tooltip("Item connected to this character.")]
        private string itemName;

        [SerializeField]
        [Tooltip("Location where the item is contextualized.")]
        private string locationName;

        [SerializeField]
        [Tooltip("Relationship source, such as Direct Contact or Proximity Contact.")]
        private string relationshipType;

        [SerializeField]
        [Tooltip("Spreadsheet source used to create this link.")]
        private string source;

        #endregion

        #region Properties

        public string CharacterName => characterName;
        public string ItemName => itemName;
        public string LocationName => locationName;
        public string RelationshipType => relationshipType;
        public string Source => source;
        public bool IsDirectContact => string.Equals(relationshipType, PawOrderEvidenceContextImporter.DirectContactRelationship, StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Constructors

        public PawOrderEvidenceContextItemLink(string characterName, string itemName, string locationName, string relationshipType, string source)
        {
            this.characterName = characterName;
            this.itemName = itemName;
            this.locationName = locationName;
            this.relationshipType = relationshipType;
            this.source = source;
        }

        #endregion
    }

    internal static class PawOrderEvidenceContextTextUtility
    {
        #region Public API

        internal static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        #endregion
    }
}
