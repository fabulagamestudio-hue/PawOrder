using System;
using System.Collections.Generic;
using Iung.Animation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class InvestigationInventoryItemView : MonoBehaviour
    {
        #region Fields

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("RectTransform controlled by the inventory manual layout.")]
        private RectTransform rectTransform;

        [SerializeField]
        [Tooltip("Button used to select this evidence item.")]
        private Button itemButton;

        [SerializeField]
        [Tooltip("Image used to display the evidence item icon.")]
        private Image iconImage;

        [SerializeField]
        [Tooltip("Text used to display the evidence item label.")]
        private TMP_Text labelText;

        [Header("AnimationProperty")]
        [SerializeField]
        [Tooltip("AnimationProperty entries used when this evidence item appears or disappears.")]
        private List<AnimationProperty> interfaceAnimations = new();

        private Action selectedCallback;

        #endregion

        #region Properties

        public RectTransform RectTransform => rectTransform;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.ConfigureStartPosition();
            }
        }

        private void OnEnable()
        {
            if (itemButton != null)
            {
                itemButton.onClick.AddListener(HandleButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (itemButton != null)
            {
                itemButton.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        #endregion

        #region Public API

        public void Bind(ItemPromptData itemPrompt, Action onSelected)
        {
            selectedCallback = onSelected;

            if (labelText != null)
            {
                labelText.text = itemPrompt != null
                    ? itemPrompt.DisplayName
                    : string.Empty;
            }

            if (iconImage != null)
            {
                iconImage.sprite = itemPrompt != null
                    ? itemPrompt.Icon
                    : null;

                iconImage.enabled = iconImage.sprite != null;
            }
        }

        public void Show()
        {
            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.RevealThis();
            }
        }

        public void Hide(Action onComplete = null)
        {
            if (interfaceAnimations.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            interfaceAnimations.HideThis(onComplete);
        }

        #endregion

        #region Internal Logic

        private void HandleButtonClicked()
        {
            selectedCallback?.Invoke();
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            RectTransform newRectTransform,
            Button newItemButton,
            Image newIconImage,
            TMP_Text newLabelText)
        {
            rectTransform = newRectTransform;
            itemButton = newItemButton;
            iconImage = newIconImage;
            labelText = newLabelText;
        }

        #endregion
#endif
    }
}
