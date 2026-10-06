using DG.Tweening;
using UnityEngine;

namespace Iung.Animation
{
    /// <summary>
    /// Applies a pulsing scale animation around the base scale.
    /// </summary>
    public class UI_PulseScaleIdle : UI_IdleBase
    {
        [Header("Pulse (scale)")]
        [SerializeField, Tooltip("Scale pulse amount (1 = no change). Example: 1.05 means +5%.")]
        float pulseScale = 1.05f;

        [SerializeField, Tooltip("Pulse frequency (cycles per second).")]
        float pulseFrequency = 1.2f;

        protected override UIAnimationChannels UsedChannels => UIAnimationChannels.Scale;

        protected override void BuildIdle()
        {
            float half = 0.5f / Mathf.Max(0.001f, pulseFrequency);

            idleSeq = DOTween.Sequence();
            idleSeq.Append(targetRect.DOScale(baseScale * pulseScale, half).SetEase(Ease.InOutSine));
            idleSeq.Append(targetRect.DOScale(baseScale, half).SetEase(Ease.InOutSine));
            idleSeq.SetLoops(-1, LoopType.Restart);
        }
    }
}
