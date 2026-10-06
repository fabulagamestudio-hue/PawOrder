using UnityEngine;

namespace Iung.Animation
{
    /// <summary>
    /// Idle animation for UI without DOTween (pure Update).
    /// Combines Float (anchoredPosition) and Wiggle (local Z rotation) using sine phases.
    /// - Frequency control now uses Vector2 (min, max). If random mode is off, only X is used.
    /// - Each channel (Float/Wiggle) has its own randomness toggle and its own sampled frequency.
    /// - 0 Hz stops and snaps to base pose.
    /// - Runtime multipliers for amplitude/angles/frequencies without rebuilds.
    /// - Honors visibility checks and auto-pause on external tweens (AnimationProperty).
    /// </summary>
    public class UI_FloatAndWiggleIdle : UI_IdleBase
    {
        [Header("Float (position)")]
        [SerializeField, Tooltip("Enable X axis movement.")]
        private bool floatAxisX = false;

        [SerializeField, Tooltip("Enable Y axis movement.")]
        private bool floatAxisY = true;

        [SerializeField, Tooltip("Amplitude in pixels.")]
        private float floatAmplitude = 10f;

        [SerializeField, Tooltip("Float frequency range in Hz (cycles per second). If 'Randomize Float' is disabled, only X (min) is used.")]
        private Vector2 floatFrequencyHz = new Vector2(0.5f, 0.5f);

        [SerializeField, Tooltip("If true, a random float frequency within [min,max] is sampled once on build/rebuild/resample.")]
        private bool randomizeFloatFrequency = false;

        [Header("Wiggle (rotation)")]
        [SerializeField, Tooltip("Start angle (degrees) relative to base local Z.")]
        private float startAngle = -5f;

        [SerializeField, Tooltip("End angle (degrees) relative to base local Z.")]
        private float endAngle = 5f;

        [SerializeField, Tooltip("Wiggle frequency range in Hz (cycles per second). If 'Randomize Wiggle' is disabled, only X (min) is used.")]
        private Vector2 wiggleFrequencyHz = new Vector2(0.8f, 0.8f);

        [SerializeField, Tooltip("If true, a random wiggle frequency within [min,max] is sampled once on build/rebuild/resample.")]
        private bool randomizeWiggleFrequency = false;

        [Header("Time")]
        [SerializeField, Tooltip("If true, uses unscaled delta time (independent of Time.timeScale).")]
        private bool useUnscaledTime = false;

        // Base pose caches
        private Vector3 _baseEuler;

        // Runtime multipliers (can be changed at runtime)
        /// <summary>Multiplier applied to Float Amplitude.</summary>
        private float _floatAmpMul = 1f;
        /// <summary>Multiplier applied to Float Frequency.</summary>
        private float _floatFreqMul = 1f;
        /// <summary>Multiplier applied to Start/End Angle.</summary>
        private float _angleMul = 1f;
        /// <summary>Multiplier applied to Wiggle Frequency.</summary>
        private float _wiggleFreqMul = 1f;

        // Phase accumulators (radians)
        private float _floatPhase;
        private float _wigglePhase;

        // Effective sampled frequencies (Hz)
        private float _floatHz;
        private float _wiggleHz;

        // Local pause state (for external animations/grace time)
        private bool _pausedByExternal;
        private float _resumeTimer;

        protected override UIAnimationChannels UsedChannels => UIAnimationChannels.Position | UIAnimationChannels.Rotation;

        #region Public API (kept compatible + new helpers)
        /// <summary>Sets all intensity multipliers at once. Usually no rebuild is needed.</summary>
        public void SetIntensity(float floatAmpMul, float floatFreqMul, float angleMul, float wiggleFreqMul, bool rebuild = false)
        {
            _floatAmpMul = Mathf.Max(0f, floatAmpMul);
            _floatFreqMul = Mathf.Max(0f, floatFreqMul);
            _angleMul = Mathf.Max(0f, angleMul);
            _wiggleFreqMul = Mathf.Max(0f, wiggleFreqMul);

            if (rebuild) RebuildIdle();
            else ApplyRuntimeSnapsOnly();
        }

        /// <summary>Halves all intensities (useful for inactive entries).</summary>
        public void ApplyHalfForInactive(bool rebuild = false) => SetIntensity(0.5f, 0.5f, 0.5f, 0.5f, rebuild);

        /// <summary>Restores full intensities (useful for active entry).</summary>
        public void ApplyFullFromPrefab(bool rebuild = false) => SetIntensity(1f, 1f, 1f, 1f, rebuild);

        /// <summary>
        /// Sets float speed in Hz (cycles per second). Keeps backward compatibility by writing both range ends and disabling randomize.
        /// </summary>
        public void SetFloatSpeedHz(float hz, bool rebuild = false)
        {
            hz = Mathf.Max(0f, hz);
            floatFrequencyHz = new Vector2(hz, hz);
            randomizeFloatFrequency = false;

            if (rebuild) RebuildIdle();
            else
            {
                _floatHz = floatFrequencyHz.x;
                ApplyRuntimeSnapsOnly();
            }
        }

        /// <summary>
        /// Sets wiggle speed in Hz (cycles per second). Keeps backward compatibility by writing both range ends and disabling randomize.
        /// </summary>
        public void SetWiggleSpeedHz(float hz, bool rebuild = false)
        {
            hz = Mathf.Max(0f, hz);
            wiggleFrequencyHz = new Vector2(hz, hz);
            randomizeWiggleFrequency = false;

            if (rebuild) RebuildIdle();
            else
            {
                _wiggleHz = wiggleFrequencyHz.x;
                ApplyRuntimeSnapsOnly();
            }
        }

        /// <summary>Multiplies both float and wiggle speeds (useful for SlowMo/Turbo).</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            multiplier = Mathf.Max(0f, multiplier);
            _floatFreqMul = multiplier;
            _wiggleFreqMul = multiplier;
            ApplyRuntimeSnapsOnly();
        }

        /// <summary>
        /// Sets the float frequency range and randomize mode.
        /// If randomize is false, only X (min) is used as the constant Hz.
        /// </summary>
        public void SetFloatSpeedRangeHz(Vector2 range, bool randomize, bool rebuild = false)
        {
            NormalizePositiveRange(ref range);
            floatFrequencyHz = range;
            randomizeFloatFrequency = randomize;

            if (rebuild) RebuildIdle();
            else
            {
                ResampleRandomFrequencies();
                ApplyRuntimeSnapsOnly();
            }
        }

        /// <summary>
        /// Sets the wiggle frequency range and randomize mode.
        /// If randomize is false, only X (min) is used as the constant Hz.
        /// </summary>
        public void SetWiggleSpeedRangeHz(Vector2 range, bool randomize, bool rebuild = false)
        {
            NormalizePositiveRange(ref range);
            wiggleFrequencyHz = range;
            randomizeWiggleFrequency = randomize;

            if (rebuild) RebuildIdle();
            else
            {
                ResampleRandomFrequencies();
                ApplyRuntimeSnapsOnly();
            }
        }

        /// <summary>
        /// Re-samples the effective frequencies based on the current ranges and randomize toggles.
        /// Call this at runtime if you want a new random pick (e.g., on state changes).
        /// </summary>
        public void ResampleRandomFrequencies()
        {
            _floatHz = randomizeFloatFrequency
                ? Random.Range(floatFrequencyHz.x, floatFrequencyHz.y)
                : floatFrequencyHz.x;

            _wiggleHz = randomizeWiggleFrequency
                ? Random.Range(wiggleFrequencyHz.x, wiggleFrequencyHz.y)
                : wiggleFrequencyHz.x;

            _floatHz = Mathf.Max(0f, _floatHz);
            _wiggleHz = Mathf.Max(0f, _wiggleHz);
        }

        /// <summary>Kills and rebuilds with current fields (only when structure changes).</summary>
        public void RebuildIdle()
        {
            // No DOTween to kill here. Just reset phases and base pose.
            BuildIdle();
        }

        /// <summary>Re-captures the current base anchored position and local euler as new base pose.</summary>
        public override void RefreshBasePose()
        {
            if (!targetRect) return;
            baseAnchoredPos = targetRect.anchoredPosition;
            _baseEuler = targetRect.localEulerAngles;
            baseZRotation = _baseEuler.z;
            ApplyRuntimeSnapsOnly();
        }
        #endregion

        /// <summary>
        /// Sets initial base pose, resets phases, and samples effective frequencies.
        /// </summary>
        protected override void BuildIdle()
        {
            if (!targetRect) return;

            _baseEuler = targetRect.localEulerAngles;
            baseZRotation = _baseEuler.z;

            _floatPhase = 0f;
            _wigglePhase = 0f;

            NormalizePositiveRange(ref floatFrequencyHz);
            NormalizePositiveRange(ref wiggleFrequencyHz);
            ResampleRandomFrequencies();

            ApplyRuntimeSnapsOnly();
        }

        /// <summary>
        /// Our own Update loop: honors visibility, external tween conflicts, and animates via phases.
        /// </summary>
        protected override void Update()
        {
            if (!IsIdleStarted)
            {
                ApplyRuntimeSnapsOnly();
                return;
            }

            // Visibility gate
            if (onlyWhenVisible && !IsVisibleNow())
            {
                // When invisible, keep snapped state for disabled channels
                ApplyRuntimeSnapsOnly();
                return;
            }

            if (HasExplicitExternalPauseOnUsedChannels())
            {
                ApplyRuntimeSnapsOnly();
                return;
            }

            // External animation (AnimationProperty / DOTween on same channels) pause
            bool externalConflict = autoPauseOnExternalTweens && IsExternallyAnimatingOnChannels(UsedChannels);
            if (externalConflict)
            {
                _pausedByExternal = true;
                _resumeTimer = resumeGraceTime;
                // While paused, maintain snap for disabled channels (and freeze phases)
                ApplyRuntimeSnapsOnly();
                return;
            }

            if (_pausedByExternal)
            {
                if (_resumeTimer > 0f)
                {
                    _resumeTimer -= Time.deltaTime;
                    ApplyRuntimeSnapsOnly();
                    return;
                }
                _pausedByExternal = false;
            }

            // Animate
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            // FLOAT channel
            float effFloatHz = _floatHz * _floatFreqMul;
            bool floatEnabled = (effFloatHz > 0f) && (floatAmplitude * _floatAmpMul > 0f) && (floatAxisX || floatAxisY);

            if (floatEnabled)
            {
                _floatPhase += (Mathf.PI * 2f) * effFloatHz * dt;
                float s = Mathf.Sin(_floatPhase);
                float amp = floatAmplitude * _floatAmpMul;

                Vector2 offset = Vector2.zero;
                if (floatAxisX) offset.x = s * amp;
                if (floatAxisY) offset.y = s * amp;

                targetRect.anchoredPosition = baseAnchoredPos + offset;
            }
            else
            {
                targetRect.anchoredPosition = baseAnchoredPos; // snap
            }

            // WIGGLE channel
            float effWiggleHz = _wiggleHz * _wiggleFreqMul;
            bool hasSpan = Mathf.Abs((endAngle * _angleMul) - (startAngle * _angleMul)) > 0.0001f;
            bool wiggleEnabled = (effWiggleHz > 0f) && hasSpan;

            if (wiggleEnabled)
            {
                _wigglePhase += (Mathf.PI * 2f) * effWiggleHz * dt;
                float sin01 = (Mathf.Sin(_wigglePhase) * 0.5f) + 0.5f; // 0..1..0
                float a0 = baseZRotation + startAngle * _angleMul;
                float a1 = baseZRotation + endAngle * _angleMul;
                float z = Mathf.LerpAngle(a0, a1, sin01);

                var e = _baseEuler; e.z = z;
                targetRect.localEulerAngles = e;
            }
            else
            {
                var e = _baseEuler; e.z = baseZRotation;
                targetRect.localEulerAngles = e; // snap Z
            }
        }

        /// <summary>
        /// Applies only snap logic for disabled channels (no phase advance).
        /// Uses the currently sampled effective frequencies.
        /// </summary>
        private void ApplyRuntimeSnapsOnly()
        {
            if (!targetRect) return;

            // Float snap
            float effFloatHz = _floatHz * _floatFreqMul;
            bool floatEnabled = (effFloatHz > 0f) && (floatAmplitude * _floatAmpMul > 0f) && (floatAxisX || floatAxisY);
            if (!floatEnabled)
                targetRect.anchoredPosition = baseAnchoredPos;

            // Wiggle snap
            float effWiggleHz = _wiggleHz * _wiggleFreqMul;
            bool hasSpan = Mathf.Abs((endAngle * _angleMul) - (startAngle * _angleMul)) > 0.0001f;
            bool wiggleEnabled = (effWiggleHz > 0f) && hasSpan;
            if (!wiggleEnabled)
            {
                var e = _baseEuler; e.z = baseZRotation;
                targetRect.localEulerAngles = e;
            }
        }

        /// <summary>
        /// Draws float ranges and wiggle arc using current multipliers (scene gizmos).
        /// </summary>
        protected override void DrawBehaviorGizmos(RectTransform rt)
        {
            if (!rt) return;

            float effFloatAmplitude = floatAmplitude * _floatAmpMul;
            float effStartAngle = startAngle * _angleMul;
            float effEndAngle = endAngle * _angleMul;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.75f);
            Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
            Vector3 right = rt.transform.right;
            Vector3 up = rt.transform.up;

            if (floatAxisX)
            {
                Gizmos.DrawLine(worldCenter - right * effFloatAmplitude, worldCenter + right * effFloatAmplitude);
                Gizmos.DrawSphere(worldCenter - right * effFloatAmplitude, 2f);
                Gizmos.DrawSphere(worldCenter + right * effFloatAmplitude, 2f);
            }
            if (floatAxisY)
            {
                Gizmos.DrawLine(worldCenter - up * effFloatAmplitude, worldCenter + up * effFloatAmplitude);
                Gizmos.DrawSphere(worldCenter - up * effFloatAmplitude, 2f);
                Gizmos.DrawSphere(worldCenter + up * effFloatAmplitude, 2f);
            }

            float r = Mathf.Max(rt.rect.width, rt.rect.height) * 0.15f;
            float a0 = effStartAngle * Mathf.Deg2Rad;
            float a1 = effEndAngle * Mathf.Deg2Rad;

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

        /// <summary>
        /// Ensures range is non-negative and ordered as (min<=max).
        /// </summary>
        private static void NormalizePositiveRange(ref Vector2 range)
        {
            float a = Mathf.Max(0f, range.x);
            float b = Mathf.Max(0f, range.y);
            if (a <= b) { range = new Vector2(a, b); }
            else { range = new Vector2(b, a); }
        }
    }
}
