using System.Collections.Generic;

namespace Fabula.PawOrder
{
    public sealed class QuestionPromptTargetMenu
    {
        #region Constructors

        public QuestionPromptTargetMenu(
            string targetId,
            string targetLabel,
            IReadOnlyList<QuestionPromptMenuEntry> entries)
        {
            TargetId = targetId;
            TargetLabel = targetLabel;
            Entries = entries;
        }

        #endregion

        #region Properties

        public string TargetId { get; }
        public string TargetLabel { get; }
        public IReadOnlyList<QuestionPromptMenuEntry> Entries { get; }

        #endregion
    }
}
