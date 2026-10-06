using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Fabula.PawOrder
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class ClueMagnifierDragController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Canvas that contains the magnifier RectTransform.")]
        private Canvas targetCanvas;

        [SerializeField]
        [Tooltip("RectTransform used as the local drag reference. When empty, the magnifier parent RectTransform is used.")]
        private RectTransform dragReferenceRectTransform;

        [SerializeField]
        [Tooltip("Room travel menu reveal controller blocked while the magnifier is being dragged.")]
        private RoomTravelMenuRevealController roomTravelMenuRevealController;

        [Header("Drag")]
        [SerializeField]
        [Tooltip("When enabled, the magnifier returns to its initial anchored position after the drag ends.")]
        private bool returnToStartOnEndDrag = true;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Duration used when the magnifier returns to its initial anchored position after drag ends.")]
        private float returnDuration = 0.2f;

        [SerializeField]
        [Tooltip("Ease used when the magnifier returns to its initial anchored position after drag ends.")]
        private Ease returnEase = Ease.OutCubic;

        private RectTransform magnifierRectTransform;
        private Vector2 initialAnchoredPosition;
        private Vector2 currentScreenPosition;
        private bool isDragging;
        private Tween returnTween;

        #endregion

        #region Properties

        public Vector2 CurrentScreenPosition => currentScreenPosition;
        public bool IsDragging => isDragging;

        #endregion

        #region Events

        public event Action DragStarted;
        public event Action DragEnded;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            magnifierRectTransform = (RectTransform)transform;
            initialAnchoredPosition = magnifierRectTransform.anchoredPosition;

            if (dragReferenceRectTransform == null)
            {
                dragReferenceRectTransform = magnifierRectTransform.parent as RectTransform;
            }
        }

        private void Start()
        {
            RefreshScreenPosition();
        }

        private void OnDisable()
        {
            isDragging = false;
            UnlockRoomTravelMenu();
        }

        private void OnDestroy()
        {
            KillReturnTween();
            UnlockRoomTravelMenu();
        }

        #endregion

        #region Public API

        public void OnBeginDrag(PointerEventData eventData)
        {
            KillReturnTween();
            isDragging = true;
            LockRoomTravelMenu();
            DragStarted?.Invoke();
            UpdateDragPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateDragPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            UpdateDragPosition(eventData);
            UnlockRoomTravelMenu();
            DragEnded?.Invoke();

            if (returnToStartOnEndDrag)
            {
                ReturnToInitialPosition();
            }
        }

        #endregion

        #region Internal Logic


        private void LockRoomTravelMenu()
        {
            if (roomTravelMenuRevealController == null)
            {
                return;
            }

            roomTravelMenuRevealController.SetRegionBlocked(true);
            roomTravelMenuRevealController.ForceHideMenu();
        }

        private void UnlockRoomTravelMenu()
        {
            if (roomTravelMenuRevealController == null)
            {
                return;
            }

            roomTravelMenuRevealController.SetRegionBlocked(false);
        }

        private void UpdateDragPosition(PointerEventData eventData)
        {
            if (dragReferenceRectTransform == null || eventData == null)
            {
                return;
            }

            Camera eventCamera = GetEventCamera(eventData);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragReferenceRectTransform,
                    eventData.position,
                    eventCamera,
                    out Vector2 localPointerPosition))
            {
                magnifierRectTransform.anchoredPosition = localPointerPosition;
                currentScreenPosition = eventData.position;
            }
        }

        private Camera GetEventCamera(PointerEventData eventData)
        {
            if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (targetCanvas.worldCamera != null)
            {
                return targetCanvas.worldCamera;
            }

            return eventData.pressEventCamera;
        }

        private void RefreshScreenPosition()
        {
            Camera eventCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera
                : null;

            currentScreenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, magnifierRectTransform.position);
        }

        #endregion

        #region DoTween

        private void ReturnToInitialPosition()
        {
            if (magnifierRectTransform == null)
            {
                return;
            }

            KillReturnTween();

            returnTween = magnifierRectTransform
                .DOAnchorPos(initialAnchoredPosition, Mathf.Max(0f, returnDuration))
                .SetEase(returnEase)
                .OnUpdate(RefreshScreenPosition)
                .OnComplete(() =>
                {
                    RefreshScreenPosition();
                    returnTween = null;
                });
        }

        private void KillReturnTween()
        {
            if (returnTween == null)
            {
                return;
            }

            returnTween.Kill();
            returnTween = null;
        }

        #endregion
    }
}
