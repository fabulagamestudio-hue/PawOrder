using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Iung.Animation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class QuestionBranchMenuRuntimeView : MonoBehaviour
    {
        #region Fields

        [Header("Data")]
        [SerializeField]
        [Tooltip("Optional runtime state used when ShowForCharacter is called without an explicit state.")]
        private CaseRuntimeState runtimeState;

        [SerializeField]
        [Tooltip("Includes hidden questions in the runtime UI. This is useful for debugging only.")]
        private bool includeHiddenQuestions;

        [SerializeField]
        [Tooltip("Excludes item-related questions from the general interrogation menu.")]
        private bool excludeItemQuestionsFromGeneralMenu = true;

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Root canvas group used to fade the menu in and out.")]
        private CanvasGroup rootCanvasGroup;

        [SerializeField]
        [Tooltip("Optional title text displayed at the top of the menu.")]
        private TMP_Text titleText;

        [SerializeField]
        [Tooltip("Optional subtitle text used to show the currently selected NPC.")]
        private TMP_Text subtitleText;

        [SerializeField]
        [Tooltip("Root transform where runtime columns are created.")]
        private RectTransform columnsRoot;

        [SerializeField]
        [Tooltip("Prefab used to instantiate each menu option.")]
        private QuestionBranchMenuOptionView optionTemplate;

        [Header("Manual Layout")]
        [SerializeField]
        [Tooltip("Fixed size applied to every runtime branch column and its vertical scroll viewport.")]
        private Vector2 columnSize = new Vector2(214f, 260f);

        [SerializeField]
        [Tooltip("Fixed size applied to every option RectTransform.")]
        private Vector2 optionSize = new Vector2(180f, 42f);

        [SerializeField]
        [Tooltip("Horizontal distance between each depth column.")]
        private float columnSpacing = 210f;

        [SerializeField]
        [Tooltip("Vertical distance between options in the same column.")]
        private float rowSpacing = 8f;

        [SerializeField]
        [Tooltip("Left offset applied to inactive sibling options after a branch is selected.")]
        private float inactiveOptionOffset = 34f;

        [SerializeField]
        [Tooltip("Left offset applied to older columns as the active branch advances.")]
        private float inactiveColumnOffset = 18f;

        [SerializeField]
        [Tooltip("Horizontal offset used when new options enter from the right.")]
        private float enterFromRightOffset = 42f;

        [SerializeField]
        [Tooltip("Scroll sensitivity applied to every vertical column ScrollRect.")]
        private float columnScrollSensitivity = 18f;

        [Header("AnimationProperty")]
        [SerializeField]
        [Tooltip("AnimationProperty entries used to show and hide the full branch menu interface.")]
        private List<AnimationProperty> interfaceAnimations = new();

        [Header("Tween")]
        [SerializeField]
        [Tooltip("Fallback fade duration used when no AnimationProperty entries are configured for interface visibility.")]
        private float fadeDuration = 0.15f;

        [SerializeField]
        [Tooltip("Duration used when a column moves between active and inactive positions.")]
        private float columnMoveDuration = 0.18f;

        [SerializeField]
        [Tooltip("Duration used by each option when it appears or changes state.")]
        private float optionTweenDuration = 0.18f;

        [SerializeField]
        [Tooltip("Delay applied between each option while a column appears.")]
        private float optionStaggerDelay = 0.035f;

        [SerializeField]
        [Tooltip("Scale used by inactive sibling options.")]
        private float inactiveOptionScale = 0.96f;

        [SerializeField]
        [Tooltip("Alpha used by inactive sibling options.")]
        private float inactiveOptionAlpha = 0.72f;

        private readonly List<QuestionBranchMenuColumn> spawnedColumns = new();
        private readonly List<QuestionBranchMenuNode> rootNodes = new();
        private readonly List<int> selectedIndicesByDepth = new();
        private CharacterSceneActor currentCharacterActor;
        private Tween visibilityTween;
        private Sequence branchTween;

        #endregion

        #region Events

        public event Action<QuestionPromptData> QuestionSelected;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.ConfigureStartPosition();
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            visibilityTween?.Kill();
            branchTween?.Kill();
        }

        #endregion

        #region Public API

        public void SetRuntimeState(CaseRuntimeState newRuntimeState)
        {
            runtimeState = newRuntimeState;
        }

        public void ShowForCharacter(CharacterSceneActor characterActor)
        {
            ShowForCharacter(characterActor, runtimeState);
        }

        public void ShowForCharacter(CharacterSceneActor characterActor, CaseRuntimeState newRuntimeState)
        {
            currentCharacterActor = characterActor;
            runtimeState = newRuntimeState;

            RebuildFromRuntimeState();
            Show();
        }

        public void BindMenus(IReadOnlyList<QuestionPromptGroupMenu> groupMenus)
        {
            RebuildFromMenus(groupMenus);
        }

        public void ShowForCharacterWithPrompts(
            CharacterSceneActor characterActor,
            CaseRuntimeState newRuntimeState,
            IEnumerable<QuestionPromptData> questionPrompts)
        {
            currentCharacterActor = characterActor;
            runtimeState = newRuntimeState;

            RebuildFromQuestionPrompts(questionPrompts);
            Show();
        }

        public void Refresh()
        {
            RebuildFromRuntimeState();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SetRootInteraction(true);

            visibilityTween?.Kill();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
            }

            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.RevealThis();
            }
        }

        public void Hide()
        {
            SetRootInteraction(false);
            visibilityTween?.Kill();

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
            visibilityTween?.Kill();

            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.ForceStartPosition();
            }

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = interfaceAnimations.Count > 0 ? rootCanvasGroup.alpha : 0f;
            }

            SetRootInteraction(false);
            gameObject.SetActive(false);
        }

        #endregion

        #region Internal Logic

        private void RebuildFromRuntimeState()
        {
            ClearColumns();
            UpdateHeader();
            rootNodes.Clear();
            selectedIndicesByDepth.Clear();

            if (!CanBuildMenu())
            {
                return;
            }

            IEnumerable<QuestionPromptData> questionPrompts = runtimeState.CaseData.AllPrompts
                .OfType<QuestionPromptData>();

            if (excludeItemQuestionsFromGeneralMenu)
            {
                questionPrompts = questionPrompts.Where(IsGeneralInterrogationQuestion);
            }

            RebuildFromQuestionPrompts(questionPrompts);
        }

        private void RebuildFromQuestionPrompts(IEnumerable<QuestionPromptData> questionPrompts)
        {
            if (questionPrompts == null || !CanBuildMenu())
            {
                ClearColumns();
                rootNodes.Clear();
                selectedIndicesByDepth.Clear();
                return;
            }

            IReadOnlyList<QuestionPromptGroupMenu> groupMenus = QuestionMenuBuilder.Build(
                questionPrompts,
                runtimeState,
                currentCharacterActor.CharacterData,
                currentCharacterActor.InteractionData,
                includeHiddenQuestions);

            RebuildFromMenus(groupMenus);
        }

        private void RebuildFromMenus(IReadOnlyList<QuestionPromptGroupMenu> groupMenus)
        {
            ClearColumns();
            rootNodes.Clear();
            selectedIndicesByDepth.Clear();

            if (groupMenus == null || columnsRoot == null || optionTemplate == null)
            {
                return;
            }

            rootNodes.AddRange(BuildRootNodes(groupMenus));
            if (rootNodes.Count == 0)
            {
                return;
            }

            SpawnColumn(rootNodes, 0, true);
            PlayColumnEntry(spawnedColumns[0]);
        }

        private bool IsGeneralInterrogationQuestion(QuestionPromptData questionPrompt)
        {
            if (questionPrompt == null)
            {
                return false;
            }

            if (questionPrompt.MenuGroup == QuestionPromptMenuGroup.Item)
            {
                return false;
            }

            return !questionPrompt.RelatedPrompts.OfType<ItemPromptData>().Any();
        }

        private void SetRootInteraction(bool isEnabled)
        {
            if (rootCanvasGroup == null)
            {
                return;
            }

            rootCanvasGroup.interactable = isEnabled;
            rootCanvasGroup.blocksRaycasts = isEnabled;
        }

        private bool CanBuildMenu()
        {
            return runtimeState != null
                && runtimeState.CaseData != null
                && currentCharacterActor != null
                && currentCharacterActor.CharacterData != null
                && columnsRoot != null
                && optionTemplate != null;
        }

        private void UpdateHeader()
        {
            if (titleText != null)
            {
                titleText.text = "Question Menu";
            }

            if (subtitleText == null)
            {
                return;
            }

            CharacterData characterData = currentCharacterActor != null
                ? currentCharacterActor.CharacterData
                : null;

            subtitleText.text = characterData != null
                ? $"Current NPC: {characterData.DisplayName}"
                : "Current NPC: none";
        }

        private IReadOnlyList<QuestionBranchMenuNode> BuildRootNodes(IReadOnlyList<QuestionPromptGroupMenu> groupMenus)
        {
            List<QuestionBranchMenuNode> nodes = new List<QuestionBranchMenuNode>();

            foreach (QuestionPromptGroupMenu groupMenu in groupMenus)
            {
                if (groupMenu == null)
                {
                    continue;
                }

                List<QuestionBranchMenuNode> targetNodes = new List<QuestionBranchMenuNode>();
                foreach (QuestionPromptTargetMenu targetMenu in groupMenu.Targets)
                {
                    if (targetMenu == null)
                    {
                        continue;
                    }

                    List<QuestionBranchMenuNode> entryNodes = targetMenu.Entries
                        .Select(BuildEntryNode)
                        .ToList();

                    targetNodes.Add(new QuestionBranchMenuNode(
                        targetMenu.TargetLabel,
                        true,
                        string.Empty,
                        false,
                        null,
                        entryNodes));
                }

                nodes.Add(new QuestionBranchMenuNode(
                    groupMenu.GroupLabel,
                    true,
                    string.Empty,
                    false,
                    null,
                    targetNodes));
            }

            return nodes;
        }

        private QuestionBranchMenuNode BuildEntryNode(QuestionPromptMenuEntry entry)
        {
            bool isAvailable = entry != null
                && entry.AvailabilityState == QuestionPromptAvailabilityState.Available;

            return new QuestionBranchMenuNode(
                entry != null ? entry.Label : string.Empty,
                isAvailable,
                GetStateLabel(entry),
                entry != null && entry.IsNew,
                entry != null ? entry.Prompt : null,
                Array.Empty<QuestionBranchMenuNode>());
        }

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

        private void HandleOptionSelected(int depth, int optionIndex, QuestionBranchMenuNode node)
        {
            if (node == null || !node.IsAvailable)
            {
                return;
            }

            branchTween?.Kill();
            PruneColumnsAfter(depth);
            SetSelectedIndex(depth, optionIndex);

            if (node.Children.Count > 0)
            {
                SpawnColumn(node.Children, depth + 1, false);
            }
            else if (node.QuestionPrompt != null)
            {
                QuestionSelected?.Invoke(node.QuestionPrompt);
            }

            AnimateBranchState();
        }

        private void SpawnColumn(IReadOnlyList<QuestionBranchMenuNode> nodes, int depth, bool immediate)
        {
            GameObject columnObject = new GameObject($"Question Branch Column {depth}", typeof(RectTransform));
            columnObject.transform.SetParent(columnsRoot, false);

            RectTransform columnRectTransform = (RectTransform)columnObject.transform;
            columnRectTransform.anchorMin = new Vector2(0f, 1f);
            columnRectTransform.anchorMax = new Vector2(0f, 1f);
            columnRectTransform.pivot = new Vector2(0f, 1f);
            columnRectTransform.sizeDelta = columnSize;

            CanvasGroup columnCanvasGroup = columnObject.AddComponent<CanvasGroup>();
            ScrollRect columnScrollRect = columnObject.AddComponent<ScrollRect>();
            columnScrollRect.horizontal = false;
            columnScrollRect.vertical = true;
            columnScrollRect.movementType = ScrollRect.MovementType.Clamped;
            columnScrollRect.inertia = true;
            columnScrollRect.scrollSensitivity = columnScrollSensitivity;
            columnScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            columnScrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            RectTransform viewportRectTransform = CreateColumnViewport(columnObject.transform);
            RectTransform contentRectTransform = CreateColumnContent(viewportRectTransform, nodes.Count);
            columnScrollRect.viewport = viewportRectTransform;
            columnScrollRect.content = contentRectTransform;

            QuestionBranchMenuColumn column = new QuestionBranchMenuColumn(
                depth,
                columnRectTransform,
                columnCanvasGroup,
                columnScrollRect,
                contentRectTransform);

            spawnedColumns.Add(column);

            for (int optionIndex = 0; optionIndex < nodes.Count; optionIndex++)
            {
                SpawnOption(column, nodes[optionIndex], optionIndex, immediate);
            }
        }

        private void SpawnOption(QuestionBranchMenuColumn column, QuestionBranchMenuNode node, int optionIndex, bool immediate)
        {
            QuestionBranchMenuOptionView optionView = Instantiate(optionTemplate, column.ContentRoot);
            optionView.gameObject.SetActive(true);

            RectTransform optionRectTransform = optionView.RectTransform;
            if (optionRectTransform != null)
            {
                optionRectTransform.anchorMin = new Vector2(0f, 1f);
                optionRectTransform.anchorMax = new Vector2(0f, 1f);
                optionRectTransform.pivot = new Vector2(0f, 1f);
                optionRectTransform.sizeDelta = optionSize;
            }

            int capturedDepth = column.Depth;
            int capturedOptionIndex = optionIndex;
            QuestionBranchMenuNode capturedNode = node;

            optionView.Bind(
                node.Label,
                node.IsAvailable,
                node.StateLabel,
                node.IsNew,
                () => HandleOptionSelected(capturedDepth, capturedOptionIndex, capturedNode));

            Vector2 basePosition = GetOptionBasePosition(optionIndex);
            Vector2 entryPosition = immediate ? basePosition : basePosition + new Vector2(enterFromRightOffset, 0f);
            float entryAlpha = immediate ? 1f : 0f;

            optionView.SetImmediateState(entryPosition, Vector3.one, entryAlpha);
            column.Options.Add(optionView);
        }

        private void PruneColumnsAfter(int depth)
        {
            for (int index = spawnedColumns.Count - 1; index > depth; index--)
            {
                QuestionBranchMenuColumn column = spawnedColumns[index];
                if (column.RectTransform != null)
                {
                    Destroy(column.RectTransform.gameObject);
                }

                spawnedColumns.RemoveAt(index);
            }

            while (selectedIndicesByDepth.Count > depth + 1)
            {
                selectedIndicesByDepth.RemoveAt(selectedIndicesByDepth.Count - 1);
            }
        }

        private void SetSelectedIndex(int depth, int optionIndex)
        {
            while (selectedIndicesByDepth.Count <= depth)
            {
                selectedIndicesByDepth.Add(-1);
            }

            selectedIndicesByDepth[depth] = optionIndex;
        }

        private void AnimateBranchState()
        {
            branchTween?.Kill();
            branchTween = DOTween.Sequence().SetUpdate(true);

            int activeDepth = Mathf.Max(0, spawnedColumns.Count - 1);

            foreach (QuestionBranchMenuColumn column in spawnedColumns)
            {
                Vector2 columnPosition = GetColumnPosition(column.Depth, activeDepth);
                branchTween.Join(column.RectTransform.DOAnchorPos(columnPosition, columnMoveDuration).SetEase(Ease.OutCubic));

                for (int optionIndex = 0; optionIndex < column.Options.Count; optionIndex++)
                {
                    QuestionBranchMenuOptionView optionView = column.Options[optionIndex];
                    bool hasSelectionAtDepth = selectedIndicesByDepth.Count > column.Depth
                        && selectedIndicesByDepth[column.Depth] >= 0;
                    bool isSelectedOption = hasSelectionAtDepth
                        && selectedIndicesByDepth[column.Depth] == optionIndex;
                    bool shouldRecede = hasSelectionAtDepth && !isSelectedOption;

                    Vector2 optionPosition = GetOptionBasePosition(optionIndex);
                    Vector3 optionScale = Vector3.one;
                    float optionAlpha = 1f;

                    if (shouldRecede)
                    {
                        optionPosition += new Vector2(-inactiveOptionOffset, 0f);
                        optionScale = Vector3.one * inactiveOptionScale;
                        optionAlpha = inactiveOptionAlpha;
                    }

                    float delay = column.Depth == activeDepth ? optionIndex * optionStaggerDelay : 0f;
                    Tween optionTween = optionView
                        .AnimateState(optionPosition, optionScale, optionAlpha, optionTweenDuration, Ease.OutCubic)
                        .SetDelay(delay);

                    branchTween.Join(optionTween);
                }
            }
        }

        private void PlayColumnEntry(QuestionBranchMenuColumn column)
        {
            if (column == null)
            {
                return;
            }

            branchTween?.Kill();
            branchTween = DOTween.Sequence().SetUpdate(true);

            column.RectTransform.anchoredPosition = GetColumnPosition(column.Depth, column.Depth);

            for (int optionIndex = 0; optionIndex < column.Options.Count; optionIndex++)
            {
                QuestionBranchMenuOptionView optionView = column.Options[optionIndex];
                Tween optionTween = optionView
                    .AnimateState(GetOptionBasePosition(optionIndex), Vector3.one, 1f, optionTweenDuration, Ease.OutCubic)
                    .SetDelay(optionIndex * optionStaggerDelay);

                branchTween.Join(optionTween);
            }
        }

        private Vector2 GetColumnPosition(int depth, int activeDepth)
        {
            float xPosition = depth * columnSpacing;
            int inactiveSteps = Mathf.Max(0, activeDepth - depth);
            xPosition -= inactiveSteps * inactiveColumnOffset;
            return new Vector2(xPosition, 0f);
        }

        private Vector2 GetOptionBasePosition(int optionIndex)
        {
            float yPosition = -optionIndex * (optionSize.y + rowSpacing);
            return new Vector2(inactiveOptionOffset, yPosition);
        }

        private RectTransform CreateColumnViewport(Transform parent)
        {
            GameObject viewportObject = new GameObject("Column Viewport", typeof(RectTransform));
            viewportObject.transform.SetParent(parent, false);

            RectTransform viewportRectTransform = (RectTransform)viewportObject.transform;
            viewportRectTransform.anchorMin = Vector2.zero;
            viewportRectTransform.anchorMax = Vector2.one;
            viewportRectTransform.pivot = new Vector2(0.5f, 0.5f);
            viewportRectTransform.offsetMin = Vector2.zero;
            viewportRectTransform.offsetMax = Vector2.zero;

            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportObject.AddComponent<RectMask2D>();
            return viewportRectTransform;
        }

        private RectTransform CreateColumnContent(RectTransform viewportRectTransform, int optionCount)
        {
            GameObject contentObject = new GameObject("Column Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportRectTransform, false);

            RectTransform contentRectTransform = (RectTransform)contentObject.transform;
            contentRectTransform.anchorMin = new Vector2(0f, 1f);
            contentRectTransform.anchorMax = new Vector2(0f, 1f);
            contentRectTransform.pivot = new Vector2(0f, 1f);
            contentRectTransform.sizeDelta = new Vector2(GetColumnWidth(), GetScrollableContentHeight(optionCount));
            contentRectTransform.anchoredPosition = Vector2.zero;
            return contentRectTransform;
        }

        private float GetColumnHeight(int optionCount)
        {
            if (optionCount <= 0)
            {
                return 0f;
            }

            return optionCount * optionSize.y + Mathf.Max(0, optionCount - 1) * rowSpacing;
        }

        private float GetScrollableContentHeight(int optionCount)
        {
            return Mathf.Max(GetColumnHeight(), GetColumnHeight(optionCount));
        }

        private float GetColumnWidth()
        {
            return Mathf.Max(0f, columnSize.x);
        }

        private float GetColumnHeight()
        {
            return Mathf.Max(0f, columnSize.y);
        }

        private void ClearColumns()
        {
            branchTween?.Kill();

            foreach (QuestionBranchMenuColumn column in spawnedColumns)
            {
                if (column != null && column.RectTransform != null)
                {
                    Destroy(column.RectTransform.gameObject);
                }
            }

            spawnedColumns.Clear();
        }

        #endregion

        #region Nested Types

        private sealed class QuestionBranchMenuNode
        {
            #region Constructors

            public QuestionBranchMenuNode(
                string label,
                bool isAvailable,
                string stateLabel,
                bool isNew,
                QuestionPromptData questionPrompt,
                IReadOnlyList<QuestionBranchMenuNode> children)
            {
                Label = label;
                IsAvailable = isAvailable;
                StateLabel = stateLabel;
                IsNew = isNew;
                QuestionPrompt = questionPrompt;
                Children = children ?? Array.Empty<QuestionBranchMenuNode>();
            }

            #endregion

            #region Properties

            public string Label { get; }
            public bool IsAvailable { get; }
            public string StateLabel { get; }
            public bool IsNew { get; }
            public QuestionPromptData QuestionPrompt { get; }
            public IReadOnlyList<QuestionBranchMenuNode> Children { get; }

            #endregion
        }

        private sealed class QuestionBranchMenuColumn
        {
            #region Fields

            public readonly List<QuestionBranchMenuOptionView> Options = new();

            #endregion

            #region Constructors

            public QuestionBranchMenuColumn(
                int depth,
                RectTransform rectTransform,
                CanvasGroup canvasGroup,
                ScrollRect scrollRect,
                RectTransform contentRoot)
            {
                Depth = depth;
                RectTransform = rectTransform;
                CanvasGroup = canvasGroup;
                ScrollRect = scrollRect;
                ContentRoot = contentRoot;
            }

            #endregion

            #region Properties

            public int Depth { get; }
            public RectTransform RectTransform { get; }
            public CanvasGroup CanvasGroup { get; }
            public ScrollRect ScrollRect { get; }
            public RectTransform ContentRoot { get; }

            #endregion
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            CanvasGroup newRootCanvasGroup,
            TMP_Text newTitleText,
            TMP_Text newSubtitleText,
            RectTransform newColumnsRoot,
            QuestionBranchMenuOptionView newOptionTemplate)
        {
            rootCanvasGroup = newRootCanvasGroup;
            titleText = newTitleText;
            subtitleText = newSubtitleText;
            columnsRoot = newColumnsRoot;
            optionTemplate = newOptionTemplate;
        }

        public void ConfigureLayoutForEditor(
            Vector2 newColumnSize,
            Vector2 newOptionSize,
            float newColumnSpacing,
            float newRowSpacing,
            float newInactiveOptionOffset,
            float newInactiveColumnOffset,
            float newEnterFromRightOffset,
            float newColumnScrollSensitivity)
        {
            columnSize = newColumnSize;
            optionSize = newOptionSize;
            columnSpacing = newColumnSpacing;
            rowSpacing = newRowSpacing;
            inactiveOptionOffset = newInactiveOptionOffset;
            inactiveColumnOffset = newInactiveColumnOffset;
            enterFromRightOffset = newEnterFromRightOffset;
            columnScrollSensitivity = newColumnScrollSensitivity;
        }

        public void ConfigureTweenForEditor(
            float newFadeDuration,
            float newColumnMoveDuration,
            float newOptionTweenDuration,
            float newOptionStaggerDelay,
            float newInactiveOptionScale,
            float newInactiveOptionAlpha)
        {
            fadeDuration = newFadeDuration;
            columnMoveDuration = newColumnMoveDuration;
            optionTweenDuration = newOptionTweenDuration;
            optionStaggerDelay = newOptionStaggerDelay;
            inactiveOptionScale = newInactiveOptionScale;
            inactiveOptionAlpha = newInactiveOptionAlpha;
        }

        #endregion
#endif
    }
}
