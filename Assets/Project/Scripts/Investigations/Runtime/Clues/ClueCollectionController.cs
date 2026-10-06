using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Fabula.PawOrder
{
    public sealed class ClueCollectionController : MonoBehaviour
    {
        #region Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("UI drag controller used as the magnifier source position.")]
        private ClueMagnifierDragController magnifierDragController;

        [SerializeField]
        [Tooltip("Fill image displayed on the magnifier while collecting a clue.")]
        private Image magnifierProgressFillImage;

        [SerializeField]
        [Tooltip("World clues available in the current scene.")]
        private WorldClueCollectible[] worldClues = Array.Empty<WorldClueCollectible>();

        [Header("Runtime State")]
        [SerializeField]
        [Tooltip("Optional runtime state. When assigned, collected clues are registered automatically.")]
        private CaseRuntimeState caseRuntimeState;

        [Header("World Projection")]
        [SerializeField]
        [Tooltip("World depth used when converting the screen-space magnifier position to world-space.")]
        private float worldProjectionDepth;

        [Header("Progress")]
        [SerializeField]
        [Tooltip("Universal delay applied before the magnifier progress fill starts.")]
        private float fillStartDelay = 0.25f;

        [SerializeField]
        [Tooltip("Universal time required to collect any clue after the fill delay ends.")]
        private float universalHoldTimeToCollect = 1.5f;

        [SerializeField]
        [Tooltip("When enabled, the magnifier progress fill is hidden until a clue is being inspected.")]
        private bool hideProgressWhenInactive = true;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Duration used when the magnifier progress fill returns to empty after collection stops.")]
        private float resetFillDuration = 0.2f;

        [SerializeField]
        [Tooltip("Ease used when the magnifier progress fill returns to empty after collection stops.")]
        private Ease resetFillEase = Ease.OutCubic;

        private WorldClueCollectible activeClue;
        private Tween progressTween;
        private Tween resetProgressTween;

        #endregion

        #region Events

        public event Action<ItemPromptData> ClueCollected;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            ResetProgressVisual();
        }

        private void OnEnable()
        {
            SubscribeToClues();
            SubscribeToMagnifier();
        }

        private void Update()
        {
            UpdateCollectionDetection();
        }

        private void OnDisable()
        {
            activeClue = null;
            UnsubscribeFromClues();
            UnsubscribeFromMagnifier();
            KillProgressTween();
            KillResetProgressTween();
            ResetProgressVisual();
        }

        private void OnDestroy()
        {
            KillProgressTween();
            KillResetProgressTween();
        }

        #endregion

        #region Public API

        public void SetCaseRuntimeState(CaseRuntimeState runtimeState)
        {
            caseRuntimeState = runtimeState;
        }

        #endregion

        #region Internal Logic

        private void UpdateCollectionDetection()
        {
            if (magnifierDragController == null || !magnifierDragController.IsDragging)
            {
                ResetActiveClue();
                return;
            }

            WorldClueCollectible nearestClue = FindNearestClue(GetMagnifierWorldPosition());

            if (nearestClue == activeClue)
            {
                return;
            }

            ResetActiveClue();

            if (nearestClue == null)
            {
                return;
            }

            activeClue = nearestClue;
            BeginActiveClueCollection();
        }

        private void BeginActiveClueCollection()
        {
            if (activeClue == null || activeClue.ItemData == null)
            {
                return;
            }

            SetProgressVisibility(true);
            SetProgressFill(0f);
            KillProgressTween();
            KillResetProgressTween();

            if (magnifierProgressFillImage == null)
            {
                Debug.LogError("ClueCollectionController: Magnifier progress fill image is not assigned.");
                return;
            }

            progressTween = DOTween.Sequence()
                .AppendInterval(Mathf.Max(0f, fillStartDelay))
                .Append(magnifierProgressFillImage
                    .DOFillAmount(1f, Mathf.Max(0.01f, universalHoldTimeToCollect))
                    .SetEase(Ease.Linear))
                .OnComplete(CompleteActiveClueCollection);
        }

        private void CompleteActiveClueCollection()
        {
            if (activeClue == null || activeClue.IsCollected)
            {
                ResetActiveClue();
                return;
            }

            activeClue.Collect();
            activeClue = null;
            ResetProgressVisual(true);
        }

        private Vector2 GetMagnifierWorldPosition()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogError("ClueCollectionController: Camera.main was not found.");
                return Vector2.zero;
            }

            Vector3 screenPosition = magnifierDragController.CurrentScreenPosition;
            screenPosition.z = GetProjectionDepth(mainCamera);

            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
            return worldPosition;
        }

        private float GetProjectionDepth(Camera mainCamera)
        {
            if (Mathf.Abs(worldProjectionDepth) > Mathf.Epsilon)
            {
                return worldProjectionDepth;
            }

            return Mathf.Abs(mainCamera.transform.position.z);
        }

        private WorldClueCollectible FindNearestClue(Vector2 magnifierWorldPosition)
        {
            WorldClueCollectible nearestClue = null;
            float nearestDistance = float.MaxValue;

            foreach (WorldClueCollectible clue in worldClues)
            {
                if (clue == null || clue.IsCollected || clue.ItemData == null || !clue.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = clue.GetDistanceFrom(magnifierWorldPosition);

                if (distance > clue.InspectRadius || distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestClue = clue;
            }

            return nearestClue;
        }

        private void ResetActiveClue()
        {
            if (activeClue == null && !HasActiveProgressVisual())
            {
                return;
            }

            activeClue = null;
            ResetProgressVisual(true);
        }

        private bool HasActiveProgressVisual()
        {
            return progressTween != null
                   || resetProgressTween != null
                   || magnifierProgressFillImage != null && magnifierProgressFillImage.fillAmount > 0f;
        }

        private void SubscribeToMagnifier()
        {
            if (magnifierDragController == null)
            {
                return;
            }

            magnifierDragController.DragEnded += HandleMagnifierDragEnded;
        }

        private void UnsubscribeFromMagnifier()
        {
            if (magnifierDragController == null)
            {
                return;
            }

            magnifierDragController.DragEnded -= HandleMagnifierDragEnded;
        }

        private void SubscribeToClues()
        {
            foreach (WorldClueCollectible clue in worldClues)
            {
                if (clue != null)
                {
                    clue.Collected += HandleClueCollected;
                }
            }
        }

        private void UnsubscribeFromClues()
        {
            foreach (WorldClueCollectible clue in worldClues)
            {
                if (clue != null)
                {
                    clue.Collected -= HandleClueCollected;
                }
            }
        }

        private void HandleClueCollected(WorldClueCollectible clue, ItemPromptData itemData)
        {
            caseRuntimeState?.AddCollectedItem(itemData);
            ClueCollected?.Invoke(itemData);
        }

        private void HandleMagnifierDragEnded()
        {
            ResetActiveClue();
        }

        private void ResetProgressVisual(bool shouldAnimateFill = false)
        {
            KillProgressTween();

            if (shouldAnimateFill)
            {
                ReturnProgressFillToEmpty();
                return;
            }

            KillResetProgressTween();
            SetProgressFill(0f);
            SetProgressVisibility(!hideProgressWhenInactive);
        }

        private void SetProgressFill(float amount)
        {
            if (magnifierProgressFillImage != null)
            {
                magnifierProgressFillImage.fillAmount = Mathf.Clamp01(amount);
            }
        }

        private void SetProgressVisibility(bool isVisible)
        {
            if (magnifierProgressFillImage != null)
            {
                magnifierProgressFillImage.gameObject.SetActive(isVisible);
            }
        }

        #endregion

        #region DoTween

        private void ReturnProgressFillToEmpty()
        {
            if (magnifierProgressFillImage == null)
            {
                return;
            }

            KillResetProgressTween();
            SetProgressVisibility(true);

            resetProgressTween = magnifierProgressFillImage
                .DOFillAmount(0f, Mathf.Max(0f, resetFillDuration))
                .SetEase(resetFillEase)
                .OnComplete(() =>
                {
                    resetProgressTween = null;
                    SetProgressVisibility(!hideProgressWhenInactive);
                });
        }

        private void KillProgressTween()
        {
            if (progressTween == null)
            {
                return;
            }

            progressTween.Kill();
            progressTween = null;
        }

        private void KillResetProgressTween()
        {
            if (resetProgressTween == null)
            {
                return;
            }

            resetProgressTween.Kill();
            resetProgressTween = null;
        }

        #endregion
    }
}
