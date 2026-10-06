using DG.Tweening;
using UnityEngine;

namespace Iung.Animation
{
    /// <summary>
    /// Applies a sinusoidal float motion on anchoredPosition.
    /// </summary>
    public class UI_FloatIdle : UI_IdleBase
    {
        [Header("Float (position)")]
        [SerializeField, Tooltip("Enable X axis movement.")]
        bool floatAxisX = false;

        [SerializeField, Tooltip("Enable Y axis movement.")]
        bool floatAxisY = true;

        [SerializeField, Tooltip("Amplitude in pixels.")]
        float floatAmplitude = 10f;

        [SerializeField, Tooltip("Frequency (cycles per second).")]
        float floatFrequency = 0.5f;

        protected override UIAnimationChannels UsedChannels => UIAnimationChannels.Position;

        protected override void BuildIdle()
        {
            float duration = 1f / Mathf.Max(0.001f, floatFrequency);
            float amp = floatAmplitude;

            idleSeq = DOTween.Sequence();
            idleSeq.Append(DOVirtual.Float(0f, Mathf.PI * 2f, duration, (t) =>
            {
                Vector2 offset = Vector2.zero;
                float s = Mathf.Sin(t);
                if (floatAxisX) offset.x = s * amp;
                if (floatAxisY) offset.y = s * amp;
                targetRect.anchoredPosition = baseAnchoredPos + offset;
            }).SetEase(Ease.Linear))
            .SetLoops(-1, LoopType.Restart);
        }

        protected override void DrawBehaviorGizmos(RectTransform rt)
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.75f);
            Vector3 worldPos = rt.transform.TransformPoint(rt.rect.center);
            Vector3 right = rt.transform.right;
            Vector3 up = rt.transform.up;

            if (floatAxisX)
            {
                Gizmos.DrawLine(worldPos - right * floatAmplitude, worldPos + right * floatAmplitude);
                Gizmos.DrawSphere(worldPos - right * floatAmplitude, 2f);
                Gizmos.DrawSphere(worldPos + right * floatAmplitude, 2f);
            }
            if (floatAxisY)
            {
                Gizmos.DrawLine(worldPos - up * floatAmplitude, worldPos + up * floatAmplitude);
                Gizmos.DrawSphere(worldPos - up * floatAmplitude, 2f);
                Gizmos.DrawSphere(worldPos + up * floatAmplitude, 2f);
            }
        }
    }
}
