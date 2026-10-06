using System.Collections.Generic;
using UnityEngine;

namespace Fabula.PawOrder
{
    [CreateAssetMenu(
        fileName = "QuestionPromptData",
        menuName = "Fabula/Paw Order/Investigations/Prompts/Question Prompt")]
    public sealed class QuestionPromptData : InvestigationPromptData
    {
        #region Fields

        [Header("Question Identity")]
        [SerializeField]
        [Tooltip("Stable question identifier used by investigation systems.")]
        private string questionId;

        [SerializeField]
        [Tooltip("Marks this question as part of the initial investigation set.")]
        private bool isStartingQuestion;

        [SerializeField]
        [Tooltip("Legacy group identifier kept for compatibility with existing assets and tools.")]
        private string questionGroup;

        [SerializeField]
        [Tooltip("Prompts that are narratively or mechanically related to this question.")]
        private List<InvestigationPromptData> relatedPrompts = new();

        [Header("Menu Organization")]
        [SerializeField]
        [Tooltip("Main menu group where this question should appear.")]
        private QuestionPromptMenuGroup menuGroup = QuestionPromptMenuGroup.Character;

        [SerializeField]
        [Tooltip("Type of target used to build the submenu for this question.")]
        private QuestionPromptMenuTargetType menuTargetType = QuestionPromptMenuTargetType.None;

        [SerializeField]
        [Tooltip("Stable target id used to group this question inside its submenu, such as Character.Otto or Location.Bar.")]
        private string menuTargetId;

        [SerializeField]
        [Tooltip("Short player-facing label used by the investigation menu. Falls back to Display Name when empty.")]
        private string menuLabel;

        [SerializeField]
        [Tooltip("Uses the self pronoun when the target character matches the NPC being questioned.")]
        private bool useSelfPronounWhenTargetMatchesNpc;

        [SerializeField]
        [Tooltip("Ordering value used inside the target submenu. Lower values appear first.")]
        private int sortOrder;

        [SerializeField]
        [Tooltip("Shows this question in the menu before it becomes available, usually as a locked visible entry.")]
        private bool startsVisible;

        [SerializeField]
        [Tooltip("Marks this question as a follow-up question unlocked by investigation progress.")]
        private bool isFollowUpQuestion;

        #endregion

        #region Properties

        public string QuestionId => questionId;
        public bool IsStartingQuestion => isStartingQuestion;
        public string QuestionGroup => questionGroup;
        public IReadOnlyList<InvestigationPromptData> RelatedPrompts => relatedPrompts;
        public QuestionPromptMenuGroup MenuGroup => menuGroup;
        public QuestionPromptMenuTargetType MenuTargetType => menuTargetType;
        public string MenuTargetId => menuTargetId;
        public string MenuLabel => menuLabel;
        public bool UseSelfPronounWhenTargetMatchesNpc => useSelfPronounWhenTargetMatchesNpc;
        public int SortOrder => sortOrder;
        public bool StartsVisible => startsVisible;
        public bool IsFollowUpQuestion => isFollowUpQuestion;

        #endregion
    }
}
