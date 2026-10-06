using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fabula.PawOrder
{
    public sealed class RoomTravelMenuRevealController : MonoBehaviour
    {
        #region Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Screen area that reveals the room travel menu when the cursor is inside it.")]
        private RectTransform detectionRegion;

        [SerializeField]
        [Tooltip("RectTransform moved vertically when the room travel menu appears or disappears.")]
        private RectTransform menuRoot;

        [SerializeField]
        [Tooltip("CanvasGroup used to fade the room travel menu in and out.")]
        private CanvasGroup menuCanvasGroup;

        [SerializeField]
        [Tooltip("Optional camera transform used by the UI canvas. Leave empty for Screen Space Overlay canvases.")]
        private Transform uiCamera;

        [SerializeField]
        [Tooltip("Controller that reports the current room and the fade-in completion moment during room transitions.")]
        private RoomNavigationController roomNavigationController;

        [SerializeField]
        [Tooltip("Room travel button objects that should be hidden when they represent the current room.")]
        private List<RoomTravelDestinationButton> roomTravelDestinationButtons = new();

        [Header("Positions")]
        [SerializeField]
        [Tooltip("Anchored Y position used when the menu is hidden.")]
        private float hiddenAnchoredY = -120f;

        [SerializeField]
        [Tooltip("Anchored Y position used when the menu is visible.")]
        private float visibleAnchoredY = 0f;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Duration of the show animation in seconds.")]
        private float showDuration = 0.25f;

        [SerializeField]
        [Tooltip("Duration of the hide animation in seconds.")]
        private float hideDuration = 0.2f;

        [SerializeField]
        [Tooltip("Ease used when the menu appears.")]
        private Ease showEase = Ease.OutCubic;

        [SerializeField]
        [Tooltip("Ease used when the menu disappears.")]
        private Ease hideEase = Ease.InCubic;

        [Header("Initial State")]
        [SerializeField]
        [Tooltip("When enabled, the menu starts hidden when Play Mode begins.")]
        private bool startHidden = true;

        private Sequence menuSequence;
        private bool isMenuVisible;
        private bool isRegionBlocked;
        private bool waitForCursorExitBeforeReveal;

        #endregion

        #region Nested Types

        [Serializable]
        private sealed class RoomTravelDestinationButton
        {
            [SerializeField]
            [Tooltip("Room represented by this travel destination button.")]
            private RoomRegion room;

            [SerializeField]
            [Tooltip("GameObject that should be shown or hidden for this travel destination.")]
            private GameObject buttonObject;

            public RoomRegion Room => room;
            public GameObject ButtonObject => buttonObject;
        }

        #endregion

        #region Properties

        public bool IsMenuVisible => isMenuVisible;
        public bool IsRegionBlocked => isRegionBlocked;

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            if (roomNavigationController == null)
            {
                return;
            }

            roomNavigationController.FadeInCompleted += UpdateCurrentRoomButtonVisibility;
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            UpdateCurrentRoomButtonVisibility();

            if (startHidden)
            {
                ApplyHiddenStateInstantly();
                return;
            }

            ApplyVisibleStateInstantly();
        }

        private void Update()
        {
            if (!Application.isPlaying || isRegionBlocked)
            {
                return;
            }

            bool isCursorInsideRegion = IsCursorInsideDetectionRegion();

            if (!isCursorInsideRegion)
            {
                waitForCursorExitBeforeReveal = false;
                HideMenu();
                return;
            }

            if (waitForCursorExitBeforeReveal)
            {
                return;
            }

            ShowMenu();
        }

        private void OnDisable()
        {
            if (roomNavigationController == null)
            {
                return;
            }

            roomNavigationController.FadeInCompleted -= UpdateCurrentRoomButtonVisibility;
        }

        private void OnDestroy()
        {
            menuSequence?.Kill();
        }

        #endregion

        #region Public API

        public void SetRegionBlocked(bool shouldBlockRegion)
        {
            isRegionBlocked = shouldBlockRegion;

            if (shouldBlockRegion)
            {
                ForceHideMenu();
            }
        }

        public void ForceHideMenu()
        {
            waitForCursorExitBeforeReveal = true;
            HideMenu();
        }

        public void ShowMenu()
        {
            if (isRegionBlocked || isMenuVisible || !HasValidMenuReferences())
            {
                return;
            }

            isMenuVisible = true;
            PlayMenuTween(visibleAnchoredY, 1f, showDuration, showEase);
        }

        public void HideMenu()
        {
            if (!isMenuVisible || !HasValidMenuReferences())
            {
                return;
            }

            isMenuVisible = false;
            PlayMenuTween(hiddenAnchoredY, 0f, hideDuration, hideEase);
        }

        #endregion

        #region Internal Logic

        private void UpdateCurrentRoomButtonVisibility(RoomRegion currentRoom)
        {
            for (int buttonIndex = 0; buttonIndex < roomTravelDestinationButtons.Count; buttonIndex++)
            {
                RoomTravelDestinationButton destinationButton = roomTravelDestinationButtons[buttonIndex];
                if (destinationButton == null || destinationButton.ButtonObject == null)
                {
                    continue;
                }

                bool shouldShowButton = destinationButton.Room != currentRoom;
                destinationButton.ButtonObject.SetActive(shouldShowButton);
            }
        }

        private void UpdateCurrentRoomButtonVisibility()
        {
            if (roomNavigationController == null)
            {
                return;
            }

            UpdateCurrentRoomButtonVisibility(roomNavigationController.CurrentRoom);
        }

        private bool IsCursorInsideDetectionRegion()
        {
            if (detectionRegion == null || Pointer.current == null)
            {
                return false;
            }

            Vector2 cursorScreenPosition = Pointer.current.position.ReadValue();
            Camera canvasCamera = GetCanvasCamera();

            return RectTransformUtility.RectangleContainsScreenPoint(detectionRegion, cursorScreenPosition, canvasCamera);
        }

        private Camera GetCanvasCamera()
        {
            if (uiCamera == null)
            {
                return null;
            }

            return uiCamera.GetComponent<Camera>();
        }

        private bool HasValidMenuReferences()
        {
            return menuRoot != null && menuCanvasGroup != null;
        }

        private void ApplyHiddenStateInstantly()
        {
            if (!HasValidMenuReferences())
            {
                return;
            }

            Vector2 anchoredPosition = menuRoot.anchoredPosition;
            anchoredPosition.y = hiddenAnchoredY;
            menuRoot.anchoredPosition = anchoredPosition;
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
            isMenuVisible = false;
        }

        private void ApplyVisibleStateInstantly()
        {
            if (!HasValidMenuReferences())
            {
                return;
            }

            Vector2 anchoredPosition = menuRoot.anchoredPosition;
            anchoredPosition.y = visibleAnchoredY;
            menuRoot.anchoredPosition = anchoredPosition;
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
            isMenuVisible = true;
        }

        private void SetMenuInteraction(bool shouldEnableInteraction)
        {
            if (menuCanvasGroup == null)
            {
                return;
            }

            menuCanvasGroup.interactable = shouldEnableInteraction;
            menuCanvasGroup.blocksRaycasts = shouldEnableInteraction;
        }

        #endregion

        #region DoTween

        private void PlayMenuTween(float targetAnchoredY, float targetAlpha, float duration, Ease ease)
        {
            if (!Application.isPlaying || !HasValidMenuReferences())
            {
                return;
            }

            menuSequence?.Kill();
            SetMenuInteraction(targetAlpha > 0f);

            menuSequence = DOTween.Sequence();
            menuSequence.Join(menuRoot.DOAnchorPosY(targetAnchoredY, Mathf.Max(0f, duration)).SetEase(ease));
            menuSequence.Join(menuCanvasGroup.DOFade(targetAlpha, Mathf.Max(0f, duration)).SetEase(ease));
            menuSequence.OnComplete(() => SetMenuInteraction(isMenuVisible));
        }

        #endregion
    }
}
