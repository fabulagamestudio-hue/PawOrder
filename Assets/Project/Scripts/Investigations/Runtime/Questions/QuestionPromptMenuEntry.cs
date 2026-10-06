namespace Fabula.PawOrder
{
    public sealed class QuestionPromptMenuEntry
    {
        #region Constructors

        public QuestionPromptMenuEntry(
            QuestionPromptData prompt,
            string label,
            QuestionPromptAvailabilityState availabilityState,
            bool isNew)
        {
            Prompt = prompt;
            Label = label;
            AvailabilityState = availabilityState;
            IsNew = isNew;
        }

        #endregion

        #region Properties

        public QuestionPromptData Prompt { get; }
        public string Label { get; }
        public QuestionPromptAvailabilityState AvailabilityState { get; }
        public bool IsNew { get; }

        #endregion
    }
}
