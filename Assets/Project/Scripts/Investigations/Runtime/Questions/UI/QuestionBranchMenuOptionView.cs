using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class QuestionBranchMenuOptionView : MonoBehaviour
    {
        #region Fields

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("RectTransform controlled by the branching menu animation system.")]
        private RectTransform rectTransform;

        [SerializeField]
        [Tooltip("Canvas group used to fade this option during state transitions.")]
        private CanvasGroup canvasGroup;

        [SerializeField]
        [Tooltip("Button used to select this menu option.")]
        private Button optionButton;

        [SerializeField]
        [Tooltip("Text used to display the option label.")]
        private TMP_Text labelText;

        [SerializeField]
        [Tooltip("Optional text used to display locked or unavailable states.")]
        private TMP_Text stateLabelText;

        [SerializeField]
        [Tooltip("Optional visual object shown when this option represents a new question.")]
        private GameObject newBadgeObject;

        private Action selectedCallback;
        private Tween stateTween;

        #endregion

        #region Properties

        public RectTransform RectTransform => rectTransform;

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            if (optionButton != null)
            {
                optionButton.onClick.AddListener(HandleButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (optionButton != null)
            {
                optionButton.onClick.RemoveListener(HandleButtonClicked);
            }

            stateTween?.Kill();
        }

        private void OnDestroy()
        {
            stateTween?.Kill();
        }

        #endregion

        #region Public API

        public void Bind(
            string label,
            bool isAvailable,
            string stateLabel,
            bool isNew,
            Action onSelected)
        {
            selectedCallback = onSelected;

            if (labelText != null)
            {
                labelText.text = label ?? string.Empty;
            }

            if (optionButton != null)
            {
                optionButton.interactable = isAvailable;
            }

            if (stateLabelText != null)
            {
                stateLabelText.text = stateLabel ?? string.Empty;
                stateLabelText.gameObject.SetActive(!isAvailable && !string.IsNullOrWhiteSpace(stateLabel));
            }

            if (newBadgeObject != null)
            {
                newBadgeObject.SetActive(isNew);
            }
        }

        public void SetImmediateState(Vector2 anchoredPosition, Vector3 localScale, float alpha)
        {
            stateTween?.Kill();

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = anchoredPosition;
                rectTransform.localScale = localScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
        }

        public Tween AnimateState(Vector2 anchoredPosition, Vector3 localScale, float alpha, float duration, Ease ease)
        {
            stateTween?.Kill();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);

            if (rectTransform != null)
            {
                sequence.Join(rectTransform.DOAnchorPos(anchoredPosition, duration).SetEase(ease));
                sequence.Join(rectTransform.DOScale(localScale, duration).SetEase(ease));
            }

            if (canvasGroup != null)
            {
                sequence.Join(canvasGroup.DOFade(alpha, duration).SetEase(ease));
            }

            stateTween = sequence;
            return stateTween;
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
            CanvasGroup newCanvasGroup,
            Button newOptionButton,
            TMP_Text newLabelText,
            TMP_Text newStateLabelText,
            GameObject newNewBadgeObject)
        {
            rectTransform = newRectTransform;
            canvasGroup = newCanvasGroup;
            optionButton = newOptionButton;
            labelText = newLabelText;
            stateLabelText = newStateLabelText;
            newBadgeObject = newNewBadgeObject;
        }

        #endregion
#endif
    }
}
