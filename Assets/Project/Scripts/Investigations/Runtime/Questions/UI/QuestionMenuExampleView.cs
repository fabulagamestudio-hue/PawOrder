using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Fabula.PawOrder
{
    public sealed class QuestionMenuExampleView : MonoBehaviour
    {
        #region Fields

        [Header("Data")]
        [SerializeField]
        [Tooltip("Optional runtime state used when ShowForCharacter is called without an explicit state.")]
        private CaseRuntimeState runtimeState;

        [SerializeField]
        [Tooltip("Includes hidden questions in the example UI. This is useful for debugging only.")]
        private bool includeHiddenQuestions;

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Root canvas group used to fade the example menu in and out.")]
        private CanvasGroup rootCanvasGroup;

        [SerializeField]
        [Tooltip("Title text displayed at the top of the example menu.")]
        private TMP_Text titleText;

        [SerializeField]
        [Tooltip("Subtitle text used to show the currently selected NPC.")]
        private TMP_Text subtitleText;

        [SerializeField]
        [Tooltip("Parent transform where group views are instantiated.")]
        private RectTransform groupsContentRoot;

        [SerializeField]
        [Tooltip("Template used to instantiate one view for each question menu group.")]
        private QuestionMenuGroupExampleView groupTemplate;

        [Header("Tween")]
        [SerializeField]
        [Tooltip("Fade duration used when the menu is shown or hidden.")]
        private float fadeDuration = 0.15f;

        private readonly List<QuestionMenuGroupExampleView> spawnedGroups = new();
        private CharacterSceneActor currentCharacterActor;
        private Tween visibilityTween;

        #endregion

        #region Events

        public event Action<QuestionPromptData> QuestionSelected;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            HideImmediate();
        }

        private void OnDestroy()
        {
            visibilityTween?.Kill();
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

            Rebuild();
            Show();
        }

        public void Refresh()
        {
            Rebuild();
        }

        public void Show()
        {
            if (rootCanvasGroup == null)
            {
                return;
            }

            gameObject.SetActive(true);
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;

            visibilityTween?.Kill();
            visibilityTween = rootCanvasGroup
                .DOFade(1f, fadeDuration)
                .SetUpdate(true);
        }

        public void Hide()
        {
            if (rootCanvasGroup == null)
            {
                gameObject.SetActive(false);
                return;
            }

            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;

            visibilityTween?.Kill();
            visibilityTween = rootCanvasGroup
                .DOFade(0f, fadeDuration)
                .SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));
        }

        public void HideImmediate()
        {
            visibilityTween?.Kill();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        #endregion

        #region Internal Logic

        private void Rebuild()
        {
            ClearSpawnedGroups();
            UpdateHeader();

            if (!CanBuildMenu())
            {
                return;
            }

            IEnumerable<QuestionPromptData> questionPrompts = runtimeState.CaseData.AllPrompts
                .OfType<QuestionPromptData>();

            IReadOnlyList<QuestionPromptGroupMenu> groupMenus = QuestionMenuBuilder.Build(
                questionPrompts,
                runtimeState,
                currentCharacterActor.CharacterData,
                currentCharacterActor.InteractionData,
                includeHiddenQuestions);

            foreach (QuestionPromptGroupMenu groupMenu in groupMenus)
            {
                SpawnGroup(groupMenu);
            }
        }

        private bool CanBuildMenu()
        {
            return runtimeState != null
                && runtimeState.CaseData != null
                && currentCharacterActor != null
                && currentCharacterActor.CharacterData != null
                && groupsContentRoot != null
                && groupTemplate != null;
        }

        private void UpdateHeader()
        {
            if (titleText != null)
            {
                titleText.text = "Question Menu Example";
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

        private void SpawnGroup(QuestionPromptGroupMenu groupMenu)
        {
            QuestionMenuGroupExampleView groupView = Instantiate(groupTemplate, groupsContentRoot);
            groupView.gameObject.SetActive(true);
            groupView.Bind(groupMenu, HandleQuestionSelected);
            spawnedGroups.Add(groupView);
        }

        private void ClearSpawnedGroups()
        {
            foreach (QuestionMenuGroupExampleView groupView in spawnedGroups)
            {
                if (groupView != null)
                {
                    Destroy(groupView.gameObject);
                }
            }

            spawnedGroups.Clear();
        }

        private void HandleQuestionSelected(QuestionPromptData questionPrompt)
        {
            QuestionSelected?.Invoke(questionPrompt);
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            CanvasGroup newRootCanvasGroup,
            TMP_Text newTitleText,
            TMP_Text newSubtitleText,
            RectTransform newGroupsContentRoot,
            QuestionMenuGroupExampleView newGroupTemplate)
        {
            rootCanvasGroup = newRootCanvasGroup;
            titleText = newTitleText;
            subtitleText = newSubtitleText;
            groupsContentRoot = newGroupsContentRoot;
            groupTemplate = newGroupTemplate;
        }

        #endregion
#endif
    }
}
