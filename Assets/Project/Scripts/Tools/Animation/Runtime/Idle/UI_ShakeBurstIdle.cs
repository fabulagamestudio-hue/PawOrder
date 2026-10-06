using DG.Tweening;
using UnityEngine;

namespace Iung.Animation
{
    /// <summary>
    /// Triggers periodic shake bursts on anchoredPosition.
    /// </summary>
    public class UI_ShakeBurstIdle : UI_IdleBase
    {
        [Header("Shake Burst")]
        [SerializeField, Tooltip("Time between shake bursts (seconds). Use 0 to trigger immediately every cycle.")]
        float shakeInterval = 3f;

        [SerializeField, Tooltip("Shake duration (seconds).")]
        float shakeDuration = 0.25f;

        [SerializeField, Tooltip("Shake strength on anchoredPosition (pixels).")]
        float shakeStrength = 8f;

        [SerializeField, Tooltip("Shake vibrato (number of shakes).")]
        int shakeVibrato = 10;

        [Header("Debug")]
        [SerializeField, Tooltip("Print debug logs for scheduling, start, first delta, completion and kill.")]
        bool debugLogs = true;

        // Internal
        bool loggedFirstDelta;

        protected override UIAnimationChannels UsedChannels => UIAnimationChannels.Position;

        /// <summary>
        /// Build the idle routine (schedules the first shake).
        /// </summary>
        protected override void BuildIdle()
        {
            loggedFirstDelta = false;
            ScheduleNextShake();
        }

        /// <summary>
        /// Schedules the next shake based on interval; if interval <= 0, it starts immediately.
        /// </summary>
        void ScheduleNextShake()
        {
            // Kill any pending delayed call or running shake tween
            auxiliaryTween?.Kill();
            auxiliaryTween = null;

            if (targetRect == null)
            {
                if (debugLogs) Debug.Log($"[UI_ShakeBurstIdle] No targetRect. Aborting schedule.", this);
                return;
            }

            if (shakeInterval <= 0f)
            {
                if (debugLogs) Debug.Log($"[UI_ShakeBurstIdle] Interval <= 0, starting shake immediately.", this);
                StartShakeNow();
            }
            else
            {
                if (debugLogs) Debug.Log($"[UI_ShakeBurstIdle] Scheduling shake in {shakeInterval:0.###}s.", this);
                auxiliaryTween = DOVirtual.DelayedCall(shakeInterval, StartShakeNow);
            }
        }

        /// <summary>
        /// Starts the shake tween now and reschedules on complete.
        /// </summary>
        void StartShakeNow()
        {
            if (targetRect == null)
            {
                if (debugLogs) Debug.Log($"[UI_ShakeBurstIdle] StartShakeNow aborted: targetRect is null.", this);
                return;
            }

            loggedFirstDelta = false;

            if (debugLogs)
                Debug.Log($"[UI_ShakeBurstIdle] Shake START (dur={shakeDuration:0.###}, strength={shakeStrength}, vibrato={shakeVibrato}). BasePos={baseAnchoredPos}", this);

            // Create the shake tween and store it in auxiliaryTween so base KillIdle() will handle it.
            auxiliaryTween = targetRect
                .DOShakeAnchorPos(
                    shakeDuration,
                    new Vector2(shakeStrength, shakeStrength),
                    shakeVibrato,
                    90f, false, true
                )
                // IMPORTANT: mark this tween as INTERNAL to avoid conflict detection in UI_IdleBase
                .SetTarget(this)
                .OnUpdate(() =>
                {
                    if (!debugLogs || loggedFirstDelta) return;

                    Vector2 cur = targetRect.anchoredPosition;
                    Vector2 delta = cur - baseAnchoredPos;
                    if (delta.sqrMagnitude > 0.0001f)
                    {
                        loggedFirstDelta = true;
                        Debug.Log($"[UI_ShakeBurstIdle] First delta detected: Δ=({delta.x:0.###},{delta.y:0.###}) at t={Time.time:0.###}", this);
                    }
                })
                .OnComplete(() =>
                {
                    if (debugLogs) Debug.Log($"[UI_ShakeBurstIdle] Shake COMPLETE. Restoring schedule.", this);
                    ScheduleNextShake();
                })
                .OnKill(() =>
                {
                    if (debugLogs) Debug.Log($"[UI_ShakeBurstIdle] Shake KILLED (component disabled, conflict, or rebuild).", this);
                    // No reschedule here; Kill usually precedes rebuild/disable.
                });
        }

        /// <summary>
        /// Draws the shake radius gizmo around the target.
        /// </summary>
        protected override void DrawBehaviorGizmos(RectTransform rt)
        {
            if (rt == null) return;

            Vector3 worldPos = rt.position;
            float radius = Mathf.Max(0f, shakeStrength);

            Gizmos.color = Color.cyan;
            const int segments = 24;
            Vector3 prev = worldPos + Vector3.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = worldPos + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

            Gizmos.DrawLine(worldPos + Vector3.right * radius, worldPos - Vector3.right * radius);
            Gizmos.DrawLine(worldPos + Vector3.up * radius, worldPos - Vector3.up * radius);
        }
    }

    /// <summary>
    /// Extension for anchoredPosition shake mapping DOShakePosition to anchored offsets.
    /// </summary>
    internal static class RectTransformShakeExtensions
    {
        /// <summary>
        /// Shakes anchoredPosition while keeping size/scale/rotation untouched.
        /// </summary>
        public static Tweener DOShakeAnchorPos(this RectTransform rt, float duration, Vector2 strength, int vibrato, float randomness, bool snapping, bool fadeOut)
        {
            Vector3 startLocal = rt.localPosition;
            Vector2 startAnchored = rt.anchoredPosition;

            Tweener t = rt.DOShakePosition(duration, new Vector3(strength.x, strength.y, 0f), vibrato, randomness, snapping, fadeOut);
            t.OnUpdate(() =>
            {
                Vector3 delta = rt.localPosition - startLocal;
                rt.anchoredPosition = startAnchored + new Vector2(delta.x, delta.y);
                rt.localPosition = startLocal;
            })
            .OnKill(() =>
            {
                rt.anchoredPosition = startAnchored;
                rt.localPosition = startLocal;
            });

            return t;
        }
    }
}
