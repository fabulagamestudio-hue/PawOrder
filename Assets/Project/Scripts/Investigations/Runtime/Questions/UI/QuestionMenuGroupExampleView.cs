using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class QuestionMenuGroupExampleView : MonoBehaviour
    {
        #region Fields

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Button used to expand or collapse this group.")]
        private Button toggleButton;

        [SerializeField]
        [Tooltip("Text used to display the group label.")]
        private TMP_Text groupLabelText;

        [SerializeField]
        [Tooltip("Parent transform where target views are instantiated.")]
        private RectTransform targetsContentRoot;

        [SerializeField]
        [Tooltip("Canvas group used to fade the target container when this group expands or collapses.")]
        private CanvasGroup targetsContentCanvasGroup;

        [SerializeField]
        [Tooltip("Template used to instantiate one view for each target submenu.")]
        private QuestionMenuTargetExampleView targetTemplate;

        [Header("Accordion")]
        [SerializeField]
        [Tooltip("Defines whether this group starts expanded after it is bound.")]
        private bool startsExpanded = false;

        [SerializeField]
        [Tooltip("Duration used by the expand and collapse transition.")]
        private float accordionDuration = 0.15f;

        private readonly List<QuestionMenuTargetExampleView> spawnedTargets = new();
        private string currentGroupLabel;
        private bool isExpanded;
        private Tween accordionTween;

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleExpanded);
            }
        }

        private void OnDisable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(ToggleExpanded);
            }

            accordionTween?.Kill();
        }

        private void OnDestroy()
        {
            accordionTween?.Kill();
        }

        #endregion

        #region Public API

        public void Bind(QuestionPromptGroupMenu groupMenu, Action<QuestionPromptData> onQuestionSelected)
        {
            ClearSpawnedTargets();

            currentGroupLabel = groupMenu != null ? groupMenu.GroupLabel : string.Empty;
            bool shouldStartExpanded = toggleButton == null ? true : startsExpanded;
            SetExpanded(shouldStartExpanded, true);

            if (groupMenu == null || targetsContentRoot == null || targetTemplate == null)
            {
                UpdateLabel();
                return;
            }

            foreach (QuestionPromptTargetMenu targetMenu in groupMenu.Targets)
            {
                SpawnTarget(targetMenu, onQuestionSelected);
            }

            UpdateLabel();
        }

        public void ToggleExpanded()
        {
            SetExpanded(!isExpanded, false);
        }

        public void SetExpanded(bool expanded, bool immediate)
        {
            isExpanded = expanded;
            UpdateLabel();
            ApplyContentVisibility(immediate);
        }

        #endregion

        #region Internal Logic

        private void SpawnTarget(QuestionPromptTargetMenu targetMenu, Action<QuestionPromptData> onQuestionSelected)
        {
            QuestionMenuTargetExampleView targetView = Instantiate(targetTemplate, targetsContentRoot);
            targetView.gameObject.SetActive(true);
            targetView.Bind(targetMenu, onQuestionSelected);
            spawnedTargets.Add(targetView);
        }

        private void ClearSpawnedTargets()
        {
            foreach (QuestionMenuTargetExampleView targetView in spawnedTargets)
            {
                if (targetView != null)
                {
                    Destroy(targetView.gameObject);
                }
            }

            spawnedTargets.Clear();
        }

        private void UpdateLabel()
        {
            if (groupLabelText == null)
            {
                return;
            }

            string prefix = isExpanded ? "▼" : "▶";
            groupLabelText.text = $"{prefix} {currentGroupLabel}";
        }

        private void ApplyContentVisibility(bool immediate)
        {
            if (targetsContentRoot == null)
            {
                return;
            }

            accordionTween?.Kill();

            if (targetsContentCanvasGroup == null)
            {
                targetsContentRoot.gameObject.SetActive(isExpanded);
                return;
            }

            if (immediate)
            {
                targetsContentRoot.gameObject.SetActive(isExpanded);
                targetsContentCanvasGroup.alpha = isExpanded ? 1f : 0f;
                targetsContentCanvasGroup.interactable = isExpanded;
                targetsContentCanvasGroup.blocksRaycasts = isExpanded;
                return;
            }

            if (isExpanded)
            {
                targetsContentRoot.gameObject.SetActive(true);
                targetsContentCanvasGroup.interactable = true;
                targetsContentCanvasGroup.blocksRaycasts = true;
                accordionTween = targetsContentCanvasGroup
                    .DOFade(1f, accordionDuration)
                    .SetUpdate(true);
                return;
            }

            targetsContentCanvasGroup.interactable = false;
            targetsContentCanvasGroup.blocksRaycasts = false;
            accordionTween = targetsContentCanvasGroup
                .DOFade(0f, accordionDuration)
                .SetUpdate(true)
                .OnComplete(() => targetsContentRoot.gameObject.SetActive(false));
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            Button newToggleButton,
            TMP_Text newGroupLabelText,
            RectTransform newTargetsContentRoot,
            CanvasGroup newTargetsContentCanvasGroup,
            QuestionMenuTargetExampleView newTargetTemplate)
        {
            toggleButton = newToggleButton;
            groupLabelText = newGroupLabelText;
            targetsContentRoot = newTargetsContentRoot;
            targetsContentCanvasGroup = newTargetsContentCanvasGroup;
            targetTemplate = newTargetTemplate;
        }

        #endregion
#endif
    }
}
