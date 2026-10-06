using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fabula.PawOrder
{
    public sealed class QuestionMenuRuntimeConnector : MonoBehaviour
    {
        #region Fields

        [Header("Data")]
        [SerializeField]
        [Tooltip("Case data used to initialize the runtime investigation state.")]
        private CaseData caseData;

        [SerializeField]
        [Tooltip("Initializes a local runtime state from the case data during Awake.")]
        private bool initializeRuntimeStateOnAwake = true;

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Character interaction controllers that can open the question menu.")]
        private List<CharacterInteractionController> characterInteractionControllers = new();

        [SerializeField]
        [Tooltip("Branching question menu view that renders the prompt tree as horizontal depth columns.")]
        private QuestionBranchMenuRuntimeView branchQuestionMenuView;

        [SerializeField]
        [Tooltip("Legacy example question menu view. Used only when no branching menu view is assigned.")]
        private QuestionMenuExampleView questionMenuView;

        [SerializeField]
        [Tooltip("Dialogue investigation view used to display portrait, inventory, selected question and character response.")]
        private CharacterDialogueInvestigationView dialogueInvestigationView;

        [SerializeField]
        [Tooltip("Legacy response view used to display the selected interaction outcome when no dialogue investigation view is assigned.")]
        private QuestionResponseExampleView responseView;

        [SerializeField]
        [Tooltip("Clue collection controller that writes collected evidence into this connector runtime state.")]
        private ClueCollectionController clueCollectionController;

        [Header("Behaviour")]
        [SerializeField]
        [Tooltip("Refreshes the question menu after an outcome is applied.")]
        private bool refreshMenuAfterOutcome = true;

        [SerializeField]
        [Tooltip("Closes the active question menu after a final question entry is selected. Keep disabled for the integrated dialogue interface.")]
        private bool closeMenuAfterQuestionSelected;

        private readonly CaseRuntimeState runtimeState = new();
        private CharacterSceneActor selectedCharacterActor;
        private ItemPromptData selectedEvidenceItem;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            if (initializeRuntimeStateOnAwake)
            {
                InitializeRuntimeState();
            }

            AssignRuntimeStateToMenus();
            AssignRuntimeStateToClueCollectionController();
        }

        private void OnEnable()
        {
            SubscribeToCharacterControllers();
            SubscribeToMenuEvents();
            SubscribeToDialogueEvents();
            SubscribeToClueCollectionController();
        }

        private void OnDisable()
        {
            UnsubscribeFromCharacterControllers();
            UnsubscribeFromMenuEvents();
            UnsubscribeFromDialogueEvents();
            UnsubscribeFromClueCollectionController();
        }

        #endregion

        #region Public API

        public void InitializeRuntimeState()
        {
            runtimeState.InitializeFrom(caseData);
            AssignRuntimeStateToClueCollectionController();
        }

        public void OpenForCharacter(CharacterSceneActor characterActor)
        {
            if (characterActor == null || !HasAnyMenuView())
            {
                return;
            }

            selectedCharacterActor = characterActor;
            selectedEvidenceItem = null;

            if (dialogueInvestigationView != null)
            {
                dialogueInvestigationView.ShowForCharacter(selectedCharacterActor, runtimeState);
            }

            if (branchQuestionMenuView != null)
            {
                branchQuestionMenuView.ShowForCharacter(selectedCharacterActor, runtimeState);
                return;
            }

            questionMenuView.ShowForCharacter(selectedCharacterActor, runtimeState);
        }

        #endregion

        #region Internal Logic

        private void AssignRuntimeStateToMenus()
        {
            if (branchQuestionMenuView != null)
            {
                branchQuestionMenuView.SetRuntimeState(runtimeState);
            }

            if (questionMenuView != null)
            {
                questionMenuView.SetRuntimeState(runtimeState);
            }
        }

        private void AssignRuntimeStateToClueCollectionController()
        {
            if (clueCollectionController == null)
            {
                return;
            }

            clueCollectionController.SetCaseRuntimeState(runtimeState);
        }

        private bool HasAnyMenuView()
        {
            return branchQuestionMenuView != null || questionMenuView != null;
        }

        private void SubscribeToCharacterControllers()
        {
            foreach (CharacterInteractionController characterInteractionController in characterInteractionControllers)
            {
                if (characterInteractionController == null)
                {
                    continue;
                }

                characterInteractionController.CharacterSelected += HandleCharacterSelected;
            }
        }

        private void UnsubscribeFromCharacterControllers()
        {
            foreach (CharacterInteractionController characterInteractionController in characterInteractionControllers)
            {
                if (characterInteractionController == null)
                {
                    continue;
                }

                characterInteractionController.CharacterSelected -= HandleCharacterSelected;
            }
        }

        private void SubscribeToMenuEvents()
        {
            if (branchQuestionMenuView != null)
            {
                branchQuestionMenuView.QuestionSelected += HandleQuestionSelected;
            }

            if (questionMenuView != null)
            {
                questionMenuView.QuestionSelected += HandleQuestionSelected;
            }
        }

        private void SubscribeToDialogueEvents()
        {
            if (dialogueInvestigationView != null)
            {
                dialogueInvestigationView.EvidenceItemSelected += HandleEvidenceItemSelected;
            }
        }

        private void SubscribeToClueCollectionController()
        {
            if (clueCollectionController == null)
            {
                return;
            }

            clueCollectionController.ClueCollected += HandleClueCollected;
        }

        private void UnsubscribeFromMenuEvents()
        {
            if (branchQuestionMenuView != null)
            {
                branchQuestionMenuView.QuestionSelected -= HandleQuestionSelected;
            }

            if (questionMenuView != null)
            {
                questionMenuView.QuestionSelected -= HandleQuestionSelected;
            }
        }

        private void UnsubscribeFromDialogueEvents()
        {
            if (dialogueInvestigationView != null)
            {
                dialogueInvestigationView.EvidenceItemSelected -= HandleEvidenceItemSelected;
            }
        }

        private void UnsubscribeFromClueCollectionController()
        {
            if (clueCollectionController == null)
            {
                return;
            }

            clueCollectionController.ClueCollected -= HandleClueCollected;
        }

        private void HandleCharacterSelected(CharacterSceneActor characterActor)
        {
            OpenForCharacter(characterActor);
        }

        private void HandleQuestionSelected(QuestionPromptData questionPrompt)
        {
            if (selectedCharacterActor == null || questionPrompt == null)
            {
                return;
            }

            CharacterInteractionData interaction = FindAvailableInteraction(questionPrompt);
            if (interaction == null)
            {
                Debug.LogWarning($"QuestionMenuRuntimeConnector: No available interaction found for prompt '{questionPrompt.PromptId}'.", this);
                return;
            }

            ApplyInteraction(interaction, questionPrompt);
            CloseActiveMenuAfterSelection();
        }

        private void HandleEvidenceItemSelected(ItemPromptData itemPrompt)
        {
            if (selectedCharacterActor == null || branchQuestionMenuView == null || itemPrompt == null)
            {
                return;
            }

            selectedEvidenceItem = itemPrompt;
            IEnumerable<QuestionPromptData> itemQuestionPrompts = FindQuestionsRelatedToItem(itemPrompt);
            branchQuestionMenuView.ShowForCharacterWithPrompts(selectedCharacterActor, runtimeState, itemQuestionPrompts);
        }

        private void HandleClueCollected(ItemPromptData itemPrompt)
        {
            if (itemPrompt == null)
            {
                return;
            }

            RefreshInventoryView();

            if (selectedCharacterActor != null)
            {
                RefreshActiveMenu();
            }
        }

        private CharacterInteractionData FindAvailableInteraction(QuestionPromptData questionPrompt)
        {
            return selectedCharacterActor.InteractionData
                .Where(interaction => interaction != null)
                .Where(interaction => interaction.PromptUsed == questionPrompt)
                .FirstOrDefault(interaction => runtimeState.MeetsRequirements(interaction));
        }

        private void ApplyInteraction(CharacterInteractionData interaction, QuestionPromptData questionPrompt)
        {
            InteractionOutcomeData outcome = interaction.Outcome;

            runtimeState.AddUnlockedPrompt(interaction.PromptUsed);
            runtimeState.AddDiscoveredOutcome(outcome);

            if (dialogueInvestigationView != null)
            {
                dialogueInvestigationView.ShowQuestionResponse(questionPrompt, selectedCharacterActor.CharacterData, outcome);
                dialogueInvestigationView.RefreshInventory(runtimeState);
            }
            else if (responseView != null)
            {
                responseView.ShowResponse(selectedCharacterActor.CharacterData, outcome);
            }

            if (refreshMenuAfterOutcome && !closeMenuAfterQuestionSelected)
            {
                RefreshActiveMenu();
            }
        }

        private IEnumerable<QuestionPromptData> FindQuestionsRelatedToItem(ItemPromptData itemPrompt)
        {
            if (runtimeState == null || runtimeState.CaseData == null || itemPrompt == null)
            {
                return Enumerable.Empty<QuestionPromptData>();
            }

            return runtimeState.CaseData.AllPrompts
                .OfType<QuestionPromptData>()
                .Where(questionPrompt => IsQuestionRelatedToItem(questionPrompt, itemPrompt));
        }

        private bool IsQuestionRelatedToItem(QuestionPromptData questionPrompt, ItemPromptData itemPrompt)
        {
            if (questionPrompt == null || itemPrompt == null)
            {
                return false;
            }

            if (questionPrompt.RelatedPrompts.OfType<ItemPromptData>().Any(relatedItem => relatedItem == itemPrompt))
            {
                return true;
            }

            if (questionPrompt.MenuGroup != QuestionPromptMenuGroup.Item)
            {
                return false;
            }

            return string.Equals(questionPrompt.MenuTargetId, itemPrompt.PromptId, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(questionPrompt.MenuTargetId, itemPrompt.name, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(questionPrompt.MenuTargetId, itemPrompt.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshInventoryView()
        {
            if (dialogueInvestigationView == null)
            {
                return;
            }

            dialogueInvestigationView.RefreshInventory(runtimeState);
        }

        private void RefreshActiveMenu()
        {
            if (branchQuestionMenuView != null)
            {
                if (selectedEvidenceItem != null)
                {
                    branchQuestionMenuView.ShowForCharacterWithPrompts(
                        selectedCharacterActor,
                        runtimeState,
                        FindQuestionsRelatedToItem(selectedEvidenceItem));
                    return;
                }

                branchQuestionMenuView.Refresh();
                return;
            }

            if (questionMenuView != null)
            {
                questionMenuView.Refresh();
            }
        }

        private void CloseActiveMenuAfterSelection()
        {
            if (!closeMenuAfterQuestionSelected)
            {
                return;
            }

            if (branchQuestionMenuView != null)
            {
                branchQuestionMenuView.Hide();
                return;
            }

            if (questionMenuView != null)
            {
                questionMenuView.Hide();
            }
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            CaseData newCaseData,
            IReadOnlyList<CharacterInteractionController> newCharacterInteractionControllers,
            QuestionBranchMenuRuntimeView newBranchQuestionMenuView,
            QuestionResponseExampleView newResponseView)
        {
            ConfigureForEditor(newCaseData, newCharacterInteractionControllers, newBranchQuestionMenuView, null, newResponseView);
        }

        public void ConfigureForEditor(
            CaseData newCaseData,
            IReadOnlyList<CharacterInteractionController> newCharacterInteractionControllers,
            QuestionBranchMenuRuntimeView newBranchQuestionMenuView,
            CharacterDialogueInvestigationView newDialogueInvestigationView,
            QuestionResponseExampleView newResponseView)
        {
            caseData = newCaseData;
            characterInteractionControllers = newCharacterInteractionControllers != null
                ? new List<CharacterInteractionController>(newCharacterInteractionControllers)
                : new List<CharacterInteractionController>();
            branchQuestionMenuView = newBranchQuestionMenuView;
            dialogueInvestigationView = newDialogueInvestigationView;
            questionMenuView = null;
            responseView = newResponseView;
        }

        public void ConfigureForEditor(
            CaseData newCaseData,
            IReadOnlyList<CharacterInteractionController> newCharacterInteractionControllers,
            QuestionMenuExampleView newQuestionMenuView,
            QuestionResponseExampleView newResponseView)
        {
            caseData = newCaseData;
            characterInteractionControllers = newCharacterInteractionControllers != null
                ? new List<CharacterInteractionController>(newCharacterInteractionControllers)
                : new List<CharacterInteractionController>();
            branchQuestionMenuView = null;
            questionMenuView = newQuestionMenuView;
            responseView = newResponseView;
        }

        #endregion
#endif
    }
}
