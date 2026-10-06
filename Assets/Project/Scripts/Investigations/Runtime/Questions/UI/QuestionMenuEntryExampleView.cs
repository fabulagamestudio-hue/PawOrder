using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class QuestionMenuEntryExampleView : MonoBehaviour
    {
        #region Fields

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Button used to select the question.")]
        private Button questionButton;

        [SerializeField]
        [Tooltip("Text used to display the question label.")]
        private TMP_Text questionLabelText;

        [SerializeField]
        [Tooltip("Optional text used to display the locked state.")]
        private TMP_Text stateLabelText;

        [SerializeField]
        [Tooltip("Optional visual object shown when this is a newly unlocked question.")]
        private GameObject newBadgeObject;

        private QuestionPromptData boundQuestionPrompt;
        private Action<QuestionPromptData> questionSelectedCallback;

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            if (questionButton != null)
            {
                questionButton.onClick.AddListener(HandleButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (questionButton != null)
            {
                questionButton.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        #endregion

        #region Public API

        public void Bind(QuestionPromptMenuEntry entry, Action<QuestionPromptData> onQuestionSelected)
        {
            boundQuestionPrompt = entry != null ? entry.Prompt : null;
            questionSelectedCallback = onQuestionSelected;

            if (questionLabelText != null)
            {
                questionLabelText.text = entry != null ? entry.Label : string.Empty;
            }

            bool isAvailable = entry != null
                && entry.AvailabilityState == QuestionPromptAvailabilityState.Available;

            if (questionButton != null)
            {
                questionButton.interactable = isAvailable;
            }

            if (stateLabelText != null)
            {
                stateLabelText.text = GetStateLabel(entry);
                stateLabelText.gameObject.SetActive(!isAvailable);
            }

            if (newBadgeObject != null)
            {
                newBadgeObject.SetActive(entry != null && entry.IsNew);
            }
        }

        #endregion

        #region Internal Logic

        private string GetStateLabel(QuestionPromptMenuEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            switch (entry.AvailabilityState)
            {
                case QuestionPromptAvailabilityState.LockedVisible:
                    return "Locked";
                case QuestionPromptAvailabilityState.Hidden:
                    return "Hidden";
                default:
                    return string.Empty;
            }
        }

        private void HandleButtonClicked()
        {
            if (boundQuestionPrompt == null)
            {
                return;
            }

            questionSelectedCallback?.Invoke(boundQuestionPrompt);
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            Button newQuestionButton,
            TMP_Text newQuestionLabelText,
            TMP_Text newStateLabelText,
            GameObject newNewBadgeObject)
        {
            questionButton = newQuestionButton;
            questionLabelText = newQuestionLabelText;
            stateLabelText = newStateLabelText;
            newBadgeObject = newNewBadgeObject;
        }

        #endregion
#endif
    }
}
