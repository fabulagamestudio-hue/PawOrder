using UnityEngine;

namespace Fabula.PawOrder
{
    public abstract class InvestigationPromptData : ScriptableObject
    {
        [SerializeField] private string promptId;
        [SerializeField] private string displayName;
        [SerializeField] [TextArea] private string description;
        [SerializeField] private InvestigationPromptType promptType;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool visibleInPromptLists = true;

        public string PromptId => promptId;
        public string DisplayName => displayName;
        public string Description => description;
        public InvestigationPromptType PromptType => promptType;
        public Sprite Icon => icon;
        public bool VisibleInPromptLists => visibleInPromptLists;
    }
}
