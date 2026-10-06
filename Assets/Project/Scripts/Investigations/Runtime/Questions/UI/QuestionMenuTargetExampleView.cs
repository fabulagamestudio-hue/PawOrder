using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class QuestionMenuTargetExampleView : MonoBehaviour
    {
        #region Fields

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Button used to expand or collapse this target submenu.")]
        private Button toggleButton;

        [SerializeField]
        [Tooltip("Text used to display the target submenu label.")]
        private TMP_Text targetLabelText;

        [SerializeField]
        [Tooltip("Parent transform where question entry views are instantiated.")]
        private RectTransform entriesContentRoot;

        [SerializeField]
        [Tooltip("Canvas group used to fade the entries container when this target expands or collapses.")]
        private CanvasGroup entriesContentCanvasGroup;

        [SerializeField]
        [Tooltip("Template used to instantiate one view for each question entry.")]
        private QuestionMenuEntryExampleView entryTemplate;

        [Header("Accordion")]
        [SerializeField]
        [Tooltip("Defines whether this target starts expanded after it is bound.")]
        private bool startsExpanded = false;

        [SerializeField]
        [Tooltip("Duration used by the expand and collapse transition.")]
        private float accordionDuration = 0.12f;

        private readonly List<QuestionMenuEntryExampleView> spawnedEntries = new();
        private string currentTargetLabel;
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

        public void Bind(QuestionPromptTargetMenu targetMenu, Action<QuestionPromptData> onQuestionSelected)
        {
            ClearSpawnedEntries();

            currentTargetLabel = targetMenu != null ? targetMenu.TargetLabel : string.Empty;
            bool shouldStartExpanded = toggleButton == null ? true : startsExpanded;
            SetExpanded(shouldStartExpanded, true);

            if (targetMenu == null || entriesContentRoot == null || entryTemplate == null)
            {
                UpdateLabel();
                return;
            }

            foreach (QuestionPromptMenuEntry entry in targetMenu.Entries)
            {
                SpawnEntry(entry, onQuestionSelected);
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

        private void SpawnEntry(QuestionPromptMenuEntry entry, Action<QuestionPromptData> onQuestionSelected)
        {
            QuestionMenuEntryExampleView entryView = Instantiate(entryTemplate, entriesContentRoot);
            entryView.gameObject.SetActive(true);
            entryView.Bind(entry, onQuestionSelected);
            spawnedEntries.Add(entryView);
        }

        private void ClearSpawnedEntries()
        {
            foreach (QuestionMenuEntryExampleView entryView in spawnedEntries)
            {
                if (entryView != null)
                {
                    Destroy(entryView.gameObject);
                }
            }

            spawnedEntries.Clear();
        }

        private void UpdateLabel()
        {
            if (targetLabelText == null)
            {
                return;
            }

            string prefix = isExpanded ? "▼" : "▶";
            targetLabelText.text = $"{prefix} {currentTargetLabel}";
        }

        private void ApplyContentVisibility(bool immediate)
        {
            if (entriesContentRoot == null)
            {
                return;
            }

            accordionTween?.Kill();

            if (entriesContentCanvasGroup == null)
            {
                entriesContentRoot.gameObject.SetActive(isExpanded);
                return;
            }

            if (immediate)
            {
                entriesContentRoot.gameObject.SetActive(isExpanded);
                entriesContentCanvasGroup.alpha = isExpanded ? 1f : 0f;
                entriesContentCanvasGroup.interactable = isExpanded;
                entriesContentCanvasGroup.blocksRaycasts = isExpanded;
                return;
            }

            if (isExpanded)
            {
                entriesContentRoot.gameObject.SetActive(true);
                entriesContentCanvasGroup.interactable = true;
                entriesContentCanvasGroup.blocksRaycasts = true;
                accordionTween = entriesContentCanvasGroup
                    .DOFade(1f, accordionDuration)
                    .SetUpdate(true);
                return;
            }

            entriesContentCanvasGroup.interactable = false;
            entriesContentCanvasGroup.blocksRaycasts = false;
            accordionTween = entriesContentCanvasGroup
                .DOFade(0f, accordionDuration)
                .SetUpdate(true)
                .OnComplete(() => entriesContentRoot.gameObject.SetActive(false));
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            Button newToggleButton,
            TMP_Text newTargetLabelText,
            RectTransform newEntriesContentRoot,
            CanvasGroup newEntriesContentCanvasGroup,
            QuestionMenuEntryExampleView newEntryTemplate)
        {
            toggleButton = newToggleButton;
            targetLabelText = newTargetLabelText;
            entriesContentRoot = newEntriesContentRoot;
            entriesContentCanvasGroup = newEntriesContentCanvasGroup;
            entryTemplate = newEntryTemplate;
        }

        #endregion
#endif
    }
}
