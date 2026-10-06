using System.Collections.Generic;

namespace Fabula.PawOrder.Editor
{
    internal sealed class PawOrderQuestionUpdateReport
    {
        #region Fields

        private readonly List<string> messages = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> createdAssetPaths = new List<string>();

        #endregion

        #region Properties

        public IReadOnlyList<string> Messages => messages;
        public IReadOnlyList<string> Warnings => warnings;
        public IReadOnlyList<string> CreatedAssetPaths => createdAssetPaths;
        public int CreatedQuestions { get; private set; }
        public int CreatedOutcomes { get; private set; }
        public int CreatedInteractions { get; private set; }
        public int ExistingQuestions { get; private set; }
        public int ExistingOutcomes { get; private set; }
        public int ExistingInteractions { get; private set; }
        public int SkippedChangedResponses { get; private set; }

        #endregion

        #region Public API

        public void AddMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                warnings.Add(warning);
            }
        }

        public void RegisterCreatedQuestion(string assetPath)
        {
            CreatedQuestions++;
            AddCreatedAsset(assetPath);
        }

        public void RegisterCreatedOutcome(string assetPath)
        {
            CreatedOutcomes++;
            AddCreatedAsset(assetPath);
        }

        public void RegisterCreatedInteraction(string assetPath)
        {
            CreatedInteractions++;
            AddCreatedAsset(assetPath);
        }

        public void RegisterExistingQuestion()
        {
            ExistingQuestions++;
        }

        public void RegisterExistingOutcome()
        {
            ExistingOutcomes++;
        }

        public void RegisterExistingInteraction()
        {
            ExistingInteractions++;
        }

        public void RegisterSkippedChangedResponse()
        {
            SkippedChangedResponses++;
        }

        #endregion

        #region Internal Logic

        private void AddCreatedAsset(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                createdAssetPaths.Add(assetPath);
            }
        }

        #endregion
    }
}
