using DG.Tweening;
using UnityEngine;

namespace Iung.Animation
{
    /// <summary>
    /// Applies a back-and-forth rotation on Z (wiggle) between two angles relative to the base rotation.
    /// </summary>
    public class UI_WiggleIdle : UI_IdleBase
    {
        [Header("Wiggle (rotation)")]
        [SerializeField, Tooltip("Start angle (degrees) relative to the base local Z rotation.")]
        float startAngle = -5f;

        [SerializeField, Tooltip("End angle (degrees) relative to the base local Z rotation.")]
        float endAngle = 5f;

        [SerializeField, Tooltip("Wiggle frequency (cycles per second).")]
        float wiggleFrequency = 0.8f;

        // Cache of the full base euler to preserve other axes.
        Vector3 baseLocalEuler;

        protected override UIAnimationChannels UsedChannels => UIAnimationChannels.Rotation;

        /// <summary>
        /// Builds the looping wiggle sequence from base+startAngle to base+endAngle and back.
        /// </summary>
        protected override void BuildIdle()
        {
            if (!targetRect) return;

            baseLocalEuler = targetRect.localEulerAngles;

            float half = 0.5f / Mathf.Max(0.001f, wiggleFrequency);

            idleSeq = DOTween.Sequence();

            // base + start -> base + end
            idleSeq.Append(DOVirtual.Float(0f, 1f, half, v =>
            {
                float a0 = baseLocalEuler.z + startAngle;
                float a1 = baseLocalEuler.z + endAngle;
                float z = Mathf.LerpAngle(a0, a1, v);

                var e = baseLocalEuler;
                e.z = z;
                targetRect.localEulerAngles = e;
            }).SetEase(Ease.InOutSine));

            // base + end -> base + start
            idleSeq.Append(DOVirtual.Float(0f, 1f, half, v =>
            {
                float a0 = baseLocalEuler.z + endAngle;
                float a1 = baseLocalEuler.z + startAngle;
                float z = Mathf.LerpAngle(a0, a1, v);

                var e = baseLocalEuler;
                e.z = z;
                targetRect.localEulerAngles = e;
            }).SetEase(Ease.InOutSine));

            idleSeq.SetLoops(-1, LoopType.Restart);
        }

        /// <summary>
        /// Draws the arc exactly from startAngle to endAngle around the rect center.
        /// </summary>
        protected override void DrawBehaviorGizmos(RectTransform rt)
        {
            if (!rt) return;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.75f);

            Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
            float r = Mathf.Max(rt.rect.width, rt.rect.height) * 0.15f;

            // Convert relative degrees to radians range
            float a0 = startAngle * Mathf.Deg2Rad;
            float a1 = endAngle * Mathf.Deg2Rad;

            // Ensure drawing order from a0 -> a1 with steps
            int steps = 20;
            Vector3 prev = worldCenter + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * r;
            for (int i = 1; i <= steps; i++)
            {
                float t = Mathf.Lerp(a0, a1, i / (float)steps);
                Vector3 cur = worldCenter + new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * r;
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
    }
}
