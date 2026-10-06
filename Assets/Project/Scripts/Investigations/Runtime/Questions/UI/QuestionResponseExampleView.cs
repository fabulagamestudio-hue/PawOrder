using System.Collections.Generic;
using Iung.Animation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class QuestionResponseExampleView : MonoBehaviour
    {
        #region Fields

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Root canvas group used only to control response panel interaction.")]
        private CanvasGroup rootCanvasGroup;

        [SerializeField]
        [Tooltip("Text that displays the speaking character name.")]
        private TMP_Text speakerNameText;

        [SerializeField]
        [Tooltip("Text that displays the selected interaction response.")]
        private TMP_Text responseText;

        [SerializeField]
        [Tooltip("Button used to close the response panel.")]
        private Button closeButton;

        [Header("AnimationProperty")]
        [SerializeField]
        [Tooltip("AnimationProperty entries used to show and hide the response interface.")]
        private List<AnimationProperty> interfaceAnimations = new();


        #endregion

        #region Unity Messages

        private void Awake()
        {
            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.ConfigureStartPosition();
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }

        }

        #endregion

        #region Public API

        public void ShowResponse(CharacterData speakerCharacter, InteractionOutcomeData outcome)
        {
            string speakerName = speakerCharacter != null
                ? speakerCharacter.DisplayName
                : "Unknown";

            string response = outcome != null && !string.IsNullOrWhiteSpace(outcome.ResponseText)
                ? outcome.ResponseText
                : "No response text configured.";

            ShowResponse(speakerName, response);
        }

        public void ShowResponse(string speakerName, string response)
        {
            if (speakerNameText != null)
            {
                speakerNameText.text = speakerName;
            }

            if (responseText != null)
            {
                responseText.text = response;
            }

            Show();
        }

        public void Show()
        {
            gameObject.SetActive(true);

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;
            }

            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.RevealThis();
            }
        }

        public void Hide()
        {
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.HideThis(() => gameObject.SetActive(false));
                return;
            }

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
            }

            gameObject.SetActive(false);
        }

        public void HideImmediate()
        {

            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.ForceStartPosition();
            }

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = interfaceAnimations.Count > 0 ? rootCanvasGroup.alpha : 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            CanvasGroup newRootCanvasGroup,
            TMP_Text newSpeakerNameText,
            TMP_Text newResponseText,
            Button newCloseButton)
        {
            rootCanvasGroup = newRootCanvasGroup;
            speakerNameText = newSpeakerNameText;
            responseText = newResponseText;
            closeButton = newCloseButton;
        }

        #endregion
#endif
    }
}
