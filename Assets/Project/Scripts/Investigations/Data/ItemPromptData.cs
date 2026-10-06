using System.Collections.Generic;
using UnityEngine;

namespace Fabula.PawOrder
{
    [CreateAssetMenu(
        fileName = "ItemPromptData",
        menuName = "Fabula/Paw Order/Investigations/Prompts/Item Prompt")]
    public sealed class ItemPromptData : InvestigationPromptData
    {
        [SerializeField] private LocationData sourceLocation;
        [SerializeField] private float inspectRadius = 2f;
        [SerializeField] private float holdTimeToCollect = 0.25f;
        [SerializeField] private string inventoryCategory;
        [SerializeField] private List<string> tags = new();

        public LocationData SourceLocation => sourceLocation;
        public float InspectRadius => inspectRadius;
        public float HoldTimeToCollect => holdTimeToCollect;
        public string InventoryCategory => inventoryCategory;
        public IReadOnlyList<string> Tags => tags;
    }
}
