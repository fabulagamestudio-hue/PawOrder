using System;
using System.Collections.Generic;
using DG.Tweening;
using Iung.Animation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class CharacterDialogueInvestigationView : MonoBehaviour
    {
        #region Fields

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Root canvas group used only to control interaction while AnimationProperty handles interface transitions.")]
        private CanvasGroup rootCanvasGroup;

        [SerializeField]
        [Tooltip("Text used to display the active character name.")]
        private TMP_Text characterNameText;

        [SerializeField]
        [Tooltip("Image used to display the active character portrait.")]
        private Image characterPortraitImage;

        [SerializeField]
        [Tooltip("Text field that receives the selected player question.")]
        private TMP_Text selectedQuestionText;

        [SerializeField]
        [Tooltip("Text field that receives the active character response.")]
        private TMP_Text characterResponseText;

        [SerializeField]
        [Tooltip("Runtime inventory view that renders collected evidence items.")]
        private InvestigationInventoryRuntimeView inventoryView;

        [Header("Default Text")]
        [SerializeField]
        [Tooltip("Text shown before any question is selected.")]
        private string emptyQuestionText = string.Empty;

        [SerializeField]
        [Tooltip("Text shown before any character response is selected.")]
        private string emptyResponseText = string.Empty;

        [Header("AnimationProperty")]
        [SerializeField]
        [Tooltip("AnimationProperty entries used to show and hide the full dialogue interface.")]
        private List<AnimationProperty> interfaceAnimations = new();

        [SerializeField]
        [Tooltip("AnimationProperty entries used when a selected question is written to the UI.")]
        private List<AnimationProperty> selectedQuestionAnimations = new();

        [SerializeField]
        [Tooltip("AnimationProperty entries used when a character response is written to the UI.")]
        private List<AnimationProperty> characterResponseAnimations = new();

        [Header("DoTween Text")]
        [SerializeField]
        [Tooltip("Seconds used by each character during the dialogue writing animation.")]
        private float textWriteDurationPerCharacter = 0.025f;

        private Tween selectedQuestionTextTween;
        private Tween characterResponseTextTween;
        private int latestTextTweenStartFrame = -1;

        #endregion

        #region Events

        public event Action<ItemPromptData> EvidenceItemSelected;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            ConfigureAnimationStartPositions();
            HideImmediate();
        }

        private void OnEnable()
        {
            if (inventoryView != null)
            {
                inventoryView.ItemSelected += HandleInventoryItemSelected;
            }
        }

        private void Update()
        {
            if (CanSkipTextWriting() && IsAnyMouseButtonPressed())
            {
                CompleteActiveTextTweens();
            }
        }

        private void OnDisable()
        {
            if (inventoryView != null)
            {
                inventoryView.ItemSelected -= HandleInventoryItemSelected;
            }

            KillActiveTextTweens();
        }

        #endregion

        #region Public API

        public void ShowForCharacter(CharacterSceneActor characterActor, CaseRuntimeState runtimeState)
        {
            CharacterData characterData = characterActor != null
                ? characterActor.CharacterData
                : null;

            ApplyCharacterData(characterData);
            ResetDialogueTexts();
            RefreshInventory(runtimeState);
            Show();
        }

        public void RefreshInventory(CaseRuntimeState runtimeState)
        {
            if (inventoryView == null)
            {
                return;
            }

            inventoryView.ShowItems(runtimeState != null
                ? runtimeState.CollectedItems
                : Array.Empty<ItemPromptData>());
        }

        public void ShowQuestionResponse(QuestionPromptData questionPrompt, CharacterData characterData, InteractionOutcomeData outcome)
        {
            string questionText = questionPrompt != null
                ? QuestionPromptTextFormatter.GetMenuLabel(questionPrompt, characterData)
                : string.Empty;

            string responseText = outcome != null && !string.IsNullOrWhiteSpace(outcome.ResponseText)
                ? outcome.ResponseText
                : string.Empty;

            WriteText(selectedQuestionText, selectedQuestionAnimations, questionText);
            WriteText(characterResponseText, characterResponseAnimations, responseText);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SetRootInteraction(true);

            if (interfaceAnimations.Count == 0)
            {
                return;
            }

            interfaceAnimations.RevealThis();
        }

        public void Hide()
        {
            KillActiveTextTweens();
            SetRootInteraction(false);

            if (interfaceAnimations.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            interfaceAnimations.HideThis(() => gameObject.SetActive(false));
        }

        public void HideImmediate()
        {
            KillActiveTextTweens();

            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.ForceStartPosition();
            }

            SetRootInteraction(false);
            gameObject.SetActive(false);
        }

        #endregion

        #region Internal Logic

        private void ConfigureAnimationStartPositions()
        {
            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.ConfigureStartPosition();
            }

            if (selectedQuestionAnimations.Count > 0)
            {
                selectedQuestionAnimations.ConfigureStartPosition();
            }

            if (characterResponseAnimations.Count > 0)
            {
                characterResponseAnimations.ConfigureStartPosition();
            }
        }

        private void ApplyCharacterData(CharacterData characterData)
        {
            if (characterNameText != null)
            {
                characterNameText.text = characterData != null
                    ? characterData.DisplayName
                    : string.Empty;
            }

            if (characterPortraitImage != null)
            {
                characterPortraitImage.sprite = characterData != null
                    ? characterData.Portrait
                    : null;

                characterPortraitImage.enabled = characterPortraitImage.sprite != null;
            }
        }

        private void ResetDialogueTexts()
        {
            WriteText(selectedQuestionText, selectedQuestionAnimations, emptyQuestionText);
            WriteText(characterResponseText, characterResponseAnimations, emptyResponseText);
        }

        private void WriteText(TMP_Text targetText, List<AnimationProperty> animations, string value)
        {
            if (targetText == null)
            {
                return;
            }

            string safeValue = value ?? string.Empty;
            KillTextTween(targetText);

            if (animations.Count > 0)
            {
                animations.ForceStartPosition();
                animations.RevealThis();
            }

            if (string.IsNullOrEmpty(safeValue) || textWriteDurationPerCharacter <= 0f)
            {
                targetText.text = safeValue;
                return;
            }

            targetText.text = safeValue;
            targetText.ForceMeshUpdate();

            int visibleCharacterCount = targetText.textInfo.characterCount;
            targetText.maxVisibleCharacters = 0;

            Tween textTween = DOTween
                .To(
                    () => targetText.maxVisibleCharacters,
                    visibleCharacters => targetText.maxVisibleCharacters = visibleCharacters,
                    visibleCharacterCount,
                    GetTextWriteDuration(visibleCharacterCount))
                .SetEase(Ease.Linear)
                .OnComplete(() => targetText.maxVisibleCharacters = int.MaxValue);

            SetTextTween(targetText, textTween);
            latestTextTweenStartFrame = Time.frameCount;
        }

        private float GetTextWriteDuration(int visibleCharacterCount)
        {
            return Mathf.Max(0.01f, visibleCharacterCount * textWriteDurationPerCharacter);
        }

        private bool CanSkipTextWriting()
        {
            return Time.frameCount > latestTextTweenStartFrame
                && (IsTextTweenActive(selectedQuestionTextTween) || IsTextTweenActive(characterResponseTextTween));
        }

        private bool IsTextTweenActive(Tween textTween)
        {
            return textTween != null && textTween.IsActive() && textTween.IsPlaying();
        }

        private bool IsAnyMouseButtonPressed()
        {
            Mouse mouse = Mouse.current;

            return mouse != null
                && (mouse.leftButton.wasPressedThisFrame
                    || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame);
        }

        private void CompleteActiveTextTweens()
        {
            CompleteTextTween(selectedQuestionTextTween);
            CompleteTextTween(characterResponseTextTween);
        }

        private void CompleteTextTween(Tween textTween)
        {
            if (textTween == null || !textTween.IsActive())
            {
                return;
            }

            textTween.Complete();
        }

        private void KillActiveTextTweens()
        {
            KillTextTween(selectedQuestionText);
            KillTextTween(characterResponseText);
        }

        private void KillTextTween(TMP_Text targetText)
        {
            if (targetText == selectedQuestionText)
            {
                KillTextTween(ref selectedQuestionTextTween);
                return;
            }

            if (targetText == characterResponseText)
            {
                KillTextTween(ref characterResponseTextTween);
            }
        }

        private void KillTextTween(ref Tween textTween)
        {
            if (textTween == null)
            {
                return;
            }

            if (textTween.IsActive())
            {
                textTween.Kill();
            }

            textTween = null;
        }

        private void SetTextTween(TMP_Text targetText, Tween textTween)
        {
            if (targetText == selectedQuestionText)
            {
                selectedQuestionTextTween = textTween;
                return;
            }

            if (targetText == characterResponseText)
            {
                characterResponseTextTween = textTween;
            }
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

        private void HandleInventoryItemSelected(ItemPromptData itemPrompt)
        {
            EvidenceItemSelected?.Invoke(itemPrompt);
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            CanvasGroup newRootCanvasGroup,
            TMP_Text newCharacterNameText,
            Image newCharacterPortraitImage,
            TMP_Text newSelectedQuestionText,
            TMP_Text newCharacterResponseText,
            InvestigationInventoryRuntimeView newInventoryView)
        {
            rootCanvasGroup = newRootCanvasGroup;
            characterNameText = newCharacterNameText;
            characterPortraitImage = newCharacterPortraitImage;
            selectedQuestionText = newSelectedQuestionText;
            characterResponseText = newCharacterResponseText;
            inventoryView = newInventoryView;
        }

        #endregion
#endif
    }
}
