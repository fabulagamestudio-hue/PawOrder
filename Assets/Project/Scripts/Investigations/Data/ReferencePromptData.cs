using UnityEngine;

namespace Fabula.PawOrder
{
    [CreateAssetMenu(
        fileName = "ReferencePromptData",
        menuName = "Fabula/Paw Order/Investigations/Prompts/Reference Prompt")]
    public sealed class ReferencePromptData : InvestigationPromptData
    {
        [SerializeField] private string sourceSheet;
        [SerializeField] private int sourceRow;
        [SerializeField] private string topicCategory;
        [SerializeField] private CharacterData relatedCharacter;
        [SerializeField] private LocationData relatedLocation;
        [SerializeField] private ItemPromptData relatedItem;
        [SerializeField] private bool startsUnlocked;

        public string SourceSheet => sourceSheet;
        public int SourceRow => sourceRow;
        public string TopicCategory => topicCategory;
        public CharacterData RelatedCharacter => relatedCharacter;
        public LocationData RelatedLocation => relatedLocation;
        public ItemPromptData RelatedItem => relatedItem;
        public bool StartsUnlocked => startsUnlocked;
    }
}
