using DG.Tweening;
using System;
using UnityEngine;

namespace Iung.Animation
{
    [System.Serializable]
    public struct RectTransformState
    {
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 scale;
        public Quaternion rotation;
    }

    public class UI_RectTransformTweener : MonoBehaviour
    {
        [HideInInspector] public RectTransform targetRectTransform;

        public RectTransformState stateA;
        public RectTransformState stateB;

        public float transitionDuration = 0.5f;
        public Ease easeType = Ease.InOutBack;

        public bool isStateA = true;

        public void ToggleState()
        {
            ToggleState(null);
        }

        public void ToggleState(Action OnEnd = null)
        {
            if (targetRectTransform == null)
            {
                targetRectTransform = GetComponent<RectTransform>();
            }

            RectTransformState targetState = isStateA ? stateB : stateA;

            DOTween.Kill(targetRectTransform);

            targetRectTransform.DOAnchorPos(targetState.anchoredPosition, transitionDuration).SetEase(easeType);
            targetRectTransform.DOSizeDelta(targetState.sizeDelta, transitionDuration).SetEase(easeType);
            targetRectTransform.DOScale(targetState.scale, transitionDuration).SetEase(easeType);
            targetRectTransform.DORotateQuaternion(targetState.rotation, transitionDuration).SetEase(easeType);

            DOVirtual.Float(0, 1, transitionDuration, (x) => { }).OnComplete(() =>
            {
                OnEnd?.Invoke();
            });

            isStateA = !isStateA;
        }

        private bool AreStatesEqual(RectTransformState state1, RectTransformState state2)
        {
            return state1.anchoredPosition == state2.anchoredPosition &&
                   state1.sizeDelta == state2.sizeDelta &&
                   state1.scale == state2.scale &&
                   state1.rotation == state2.rotation;
        }

        public void ForceState(RectTransformState state)
        {
            if (targetRectTransform == null)
            {
                targetRectTransform = GetComponent<RectTransform>();
            }

            targetRectTransform.anchoredPosition = state.anchoredPosition;
            targetRectTransform.sizeDelta = state.sizeDelta;
            targetRectTransform.localScale = state.scale;
            targetRectTransform.rotation = state.rotation;
        }

        public void AnimateTowards(RectTransformState targetState, Action OnComplete = null)
        {
            targetRectTransform.DOAnchorPos(targetState.anchoredPosition, transitionDuration).SetEase(easeType);
            targetRectTransform.DOSizeDelta(targetState.sizeDelta, transitionDuration).SetEase(easeType);
            targetRectTransform.DOScale(targetState.scale, transitionDuration).SetEase(easeType);
            targetRectTransform.DORotateQuaternion(targetState.rotation, transitionDuration).SetEase(easeType).OnComplete(()=> OnComplete?.Invoke());

            isStateA = AreStatesEqual(targetState, stateA);
        }


    }
}
