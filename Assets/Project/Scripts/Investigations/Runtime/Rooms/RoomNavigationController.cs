using System;
using DG.Tweening;
using UnityEngine;

namespace Fabula.PawOrder
{
    public sealed class RoomNavigationController : MonoBehaviour
    {
        #region Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Root transform that will be moved between room regions. Use a camera rig parent instead of the camera itself.")]
        private Transform cameraRoot;

        [SerializeField]
        [Tooltip("Full screen CanvasGroup used to fade the screen during room transitions.")]
        private CanvasGroup fadeCanvasGroup;

        [Header("Initial State")]
        [SerializeField]
        [Tooltip("Room that starts active when the scene begins.")]
        private RoomRegion startingRoom;

        [SerializeField]
        [Tooltip("When enabled, the camera is placed at the starting room target during Awake.")]
        private bool snapToStartingRoomOnAwake = true;

        [Header("Transition")]
        [SerializeField]
        [Tooltip("Duration of each fade step in seconds.")]
        private float fadeDuration = 0.35f;

        [SerializeField]
        [Tooltip("Duration of the camera movement between room targets in seconds.")]
        private float cameraMoveDuration = 0.2f;

        [SerializeField]
        [Tooltip("Ease used by the fade animation.")]
        private Ease fadeEase = Ease.InOutSine;

        [SerializeField]
        [Tooltip("Ease used by the camera movement animation.")]
        private Ease cameraMoveEase = Ease.InOutSine;

        private RoomRegion currentRoom;
        private Sequence transitionSequence;
        private bool isTransitioning;

        #endregion

        #region Properties

        public RoomRegion CurrentRoom => currentRoom;
        public bool IsTransitioning => isTransitioning;

        #endregion

        #region Events

        public event Action<RoomRegion, RoomRegion> BeforeRoomChanged;
        public event Action<RoomRegion> FadeInCompleted;
        public event Action<RoomRegion> AfterRoomChanged;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            currentRoom = startingRoom;
            PrepareFadeCanvas();
            SnapCameraToCurrentRoomIfNeeded();
        }

        private void OnDestroy()
        {
            transitionSequence?.Kill();
        }

        #endregion

        #region Public API

        public bool CanTravelTo(RoomRegion targetRoom)
        {
            if (targetRoom == null || isTransitioning || targetRoom == currentRoom)
            {
                return false;
            }

            if (!targetRoom.HasValidCameraTarget())
            {
                return false;
            }

            return true;
        }

        public void TravelTo(RoomRegion targetRoom)
        {
            if (!CanTravelTo(targetRoom))
            {
                return;
            }

            StartTransition(targetRoom);
        }

        public void SetCurrentRoomWithoutTransition(RoomRegion room)
        {
            if (room == null || !room.HasValidCameraTarget())
            {
                return;
            }

            currentRoom = room;
            MoveCameraInstantly(room);
            AfterRoomChanged?.Invoke(currentRoom);
        }

        #endregion

        #region Internal Logic

        private void PrepareFadeCanvas()
        {
            if (fadeCanvasGroup == null)
            {
                return;
            }

            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.interactable = false;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        private void SnapCameraToCurrentRoomIfNeeded()
        {
            if (!snapToStartingRoomOnAwake || currentRoom == null || !currentRoom.HasValidCameraTarget())
            {
                return;
            }

            MoveCameraInstantly(currentRoom);
        }

        private void MoveCameraInstantly(RoomRegion room)
        {
            if (cameraRoot == null || room == null || !room.HasValidCameraTarget())
            {
                return;
            }

            cameraRoot.position = room.CameraTarget.position;
            cameraRoot.rotation = room.CameraTarget.rotation;
        }

        private void SetTransitionInputBlock(bool shouldBlockInput)
        {
            if (fadeCanvasGroup == null)
            {
                return;
            }

            fadeCanvasGroup.interactable = shouldBlockInput;
            fadeCanvasGroup.blocksRaycasts = shouldBlockInput;
        }

        #endregion

        #region DoTween

        private void StartTransition(RoomRegion targetRoom)
        {
            RoomRegion previousRoom = currentRoom;

            transitionSequence?.Kill();
            isTransitioning = true;
            SetTransitionInputBlock(true);
            BeforeRoomChanged?.Invoke(previousRoom, targetRoom);

            transitionSequence = DOTween.Sequence();
            transitionSequence.Append(CreateFadeTween(1f));
            transitionSequence.AppendCallback(() => CompleteFadeIn(targetRoom));
            transitionSequence.Append(CreateCameraMoveTween(targetRoom));
            transitionSequence.Append(CreateFadeTween(0f));
            transitionSequence.OnComplete(() => CompleteTransition(targetRoom));
            transitionSequence.OnKill(() => isTransitioning = false);
        }

        private Tween CreateFadeTween(float targetAlpha)
        {
            if (fadeCanvasGroup == null)
            {
                return DOVirtual.DelayedCall(0f, () => { });
            }

            return fadeCanvasGroup
                .DOFade(targetAlpha, Mathf.Max(0f, fadeDuration))
                .SetEase(fadeEase);
        }

        private Tween CreateCameraMoveTween(RoomRegion targetRoom)
        {
            if (cameraRoot == null || targetRoom == null || !targetRoom.HasValidCameraTarget())
            {
                return DOVirtual.DelayedCall(0f, () => { });
            }

            Transform targetTransform = targetRoom.CameraTarget;
            float duration = Mathf.Max(0f, cameraMoveDuration);

            Sequence cameraSequence = DOTween.Sequence();
            cameraSequence.Join(cameraRoot.DOMove(targetTransform.position, duration).SetEase(cameraMoveEase));
            cameraSequence.Join(cameraRoot.DORotateQuaternion(targetTransform.rotation, duration).SetEase(cameraMoveEase));

            return cameraSequence;
        }

        private void CompleteFadeIn(RoomRegion targetRoom)
        {
            currentRoom = targetRoom;
            FadeInCompleted?.Invoke(currentRoom);
        }

        private void CompleteTransition(RoomRegion targetRoom)
        {
            isTransitioning = false;
            SetTransitionInputBlock(false);
            AfterRoomChanged?.Invoke(targetRoom);
        }

        #endregion
    }
}
