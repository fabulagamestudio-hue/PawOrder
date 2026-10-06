using System;

namespace Fabula.PawOrder
{
    [Serializable]
    public sealed class PlannerNodeRuntime
    {
        public string RuntimeId;
        public PlannerNodeType NodeType;
        public ItemPromptData SourceItem;
        public InteractionOutcomeData SourceOutcome;
    }
}
