using System.Collections.Generic;

namespace Fabula.PawOrder
{
    public sealed class QuestionPromptGroupMenu
    {
        #region Constructors

        public QuestionPromptGroupMenu(
            QuestionPromptMenuGroup group,
            string groupLabel,
            IReadOnlyList<QuestionPromptTargetMenu> targets)
        {
            Group = group;
            GroupLabel = groupLabel;
            Targets = targets;
        }

        #endregion

        #region Properties

        public QuestionPromptMenuGroup Group { get; }
        public string GroupLabel { get; }
        public IReadOnlyList<QuestionPromptTargetMenu> Targets { get; }

        #endregion
    }
}
