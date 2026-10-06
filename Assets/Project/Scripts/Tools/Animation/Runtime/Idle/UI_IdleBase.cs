using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Iung.Animation
{
    /// <summary>
    /// Base class for UI idle behavior components (one behavior per component).
    /// Handles visibility checks, explicit pause requests, fallback conflict detection,
    /// pause/resume and optional gizmos hook.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class UI_IdleBase : MonoBehaviour
    {
        static readonly Dictionary<RectTransform, HashSet<UI_IdleBase>> s_registeredByTarget = new Dictionary<RectTransform, HashSet<UI_IdleBase>>();
        static int s_nextPauseToken = 1;

        [Header("General")]
        [SerializeField, Tooltip("Target RectTransform. If null, uses this component's RectTransform.")]
        protected RectTransform targetRect;

        [SerializeField, Tooltip("Optional Graphic for visibility checks (Image, TMP, etc.). If assigned, Graphic.enabled must be true.")]
        protected Graphic optionalGraphic;

        [SerializeField, Tooltip("Run idle only when the object is considered 'visible' (active, Graphic enabled, alpha/scale thresholds, canvas viewport).")]
        protected bool onlyWhenVisible = true;

        [SerializeField, Tooltip("Automatically pause idle when other tweens are animating this target on conflicting channels.")]
        protected bool autoPauseOnExternalTweens = true;

        [Header("Visibility - Alpha")]
        [SerializeField, Tooltip("If true, visibility also requires effective alpha >= Alpha Threshold. Effective alpha = CanvasGroups product * Graphic color alpha (if available).")]
        protected bool respectAlphaVisibility = true;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum effective alpha to consider visible.")]
        protected float alphaThreshold = 0.05f;

        [Header("Visibility - Scale")]
        [SerializeField, Tooltip("If true, visibility also requires effective scale >= Min Scale Threshold.")]
        protected bool respectScaleVisibility = true;

        [SerializeField, Tooltip("Minimum mean local scale (avg of X and Y) to consider visible.")]
        protected float minScaleThreshold = 0.02f;

        [Header("Visibility - Canvas Viewport")]
        [SerializeField, Tooltip("If true, requires the RectTransform to be inside the Canvas viewport (fully or partially).")]
        protected bool validateWithinCanvas = true;

        [SerializeField, Tooltip("If true, the entire Rect must be inside the Canvas viewport; otherwise partial intersection is enough.")]
        protected bool requireFullInside = false;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum ratio of the Rect area that must be inside the Canvas viewport (used when Require Full Inside is false).")]
        protected float minVisibleAreaRatio = 0.2f;

        [Header("Advanced")]
        [SerializeField, Tooltip("Delay after external animation ends before resuming idle (seconds).")]
        protected float resumeGraceTime = 0.1f;

        [SerializeField, Tooltip("Draw simple gizmos in the scene view.")]
        protected bool drawGizmos = true;

        [Header("Initial Delay")]
        [SerializeField, Tooltip("Delay before the idle starts (seconds).")]
        float initialDelaySeconds = 0f;

        [SerializeField, Tooltip("If true, randomizes the initial delay within the range below.")]
        bool randomizeInitialDelay = false;

        [SerializeField, Tooltip("Random initial delay range in seconds (min,max). Used only if 'Randomize Initial Delay' is enabled.")]
        Vector2 randomInitialDelayRangeSeconds = new Vector2(0f, 0f);

        [SerializeField, Tooltip("If true, uses unscaled time for the initial delay (independent of Time.timeScale).")]
        bool useUnscaledTimeForInitialDelay = true;

        Coroutine initialDelayCoroutine;
        bool isIdleStarted;
        readonly Dictionary<int, UIAnimationChannels> externalPauseTokens = new Dictionary<int, UIAnimationChannels>();

        /// <summary>True when the idle has started (after the optional initial delay).</summary>
        protected bool IsIdleStarted => isIdleStarted;

        protected Vector2 baseAnchoredPos;
        protected Vector3 baseScale;
        protected float baseZRotation;

        protected Sequence idleSeq;
        protected Tween auxiliaryTween;
        float resumeTimer;
        bool isPausedByExternal;
        RectTransform registeredTargetRect;

        /// <summary>
        /// The set of channels used by this behavior (must be provided by derived classes).
        /// </summary>
        protected abstract UIAnimationChannels UsedChannels { get; }

        /// <summary>
        /// Implement the actual idle setup (must set idleSeq and/or auxiliaryTween).
        /// </summary>
        protected abstract void BuildIdle();

        /// <summary>
        /// Resets transform state if needed by the derived implementation.
        /// </summary>
        protected virtual void ResetToBase()
        {
            if (targetRect == null) return;
            targetRect.anchoredPosition = baseAnchoredPos;
            targetRect.localScale = baseScale;
            var e = targetRect.localEulerAngles;
            e.z = baseZRotation;
            targetRect.localEulerAngles = e;
        }

        /// <summary>
        /// Re-captures the current transform as the new base pose for this idle.
        /// </summary>
        public virtual void RefreshBasePose()
        {
            CacheBaseState();
        }

        /// <summary>
        /// Optional gizmos hook for derived classes.
        /// </summary>
        protected virtual void DrawBehaviorGizmos(RectTransform rt) { }

        protected virtual void Awake()
        {
            EnsureTargetAssigned();
            if (targetRect == null)
            {
                Debug.LogWarning("[" + GetType().Name + "] Missing RectTransform.");
                enabled = false;
                return;
            }

            CacheBaseState();
        }

        protected virtual void OnEnable()
        {
            RefreshTargetRegistration();
            BeginStartIdleFlow();
        }

        protected virtual void OnDisable()
        {
            if (initialDelayCoroutine != null)
            {
                StopCoroutine(initialDelayCoroutine);
                initialDelayCoroutine = null;
            }

            isIdleStarted = false;
            ReleaseAllExternalPauseTokens();
            UnregisterTarget();
            KillIdle();
            ResetToBase();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            EnsureTargetAssigned();
            if (Application.isPlaying && isActiveAndEnabled)
            {
                RefreshTargetRegistration();
                CacheBaseState();
            }
        }
#endif

        /// <summary>
        /// Starts or rebuilds the idle behavior.
        /// </summary>
        protected void StartIdle()
        {
            KillIdle();
            BuildIdle();
            isIdleStarted = true;
        }

        /// <summary>
        /// Starts the idle flow respecting the optional initial delay.
        /// </summary>
        void BeginStartIdleFlow()
        {
            if (initialDelayCoroutine != null)
            {
                StopCoroutine(initialDelayCoroutine);
                initialDelayCoroutine = null;
            }

            isIdleStarted = false;
            KillIdle();
            ResetToBase();

            initialDelayCoroutine = StartCoroutine(Cor_StartIdleAfterInitialDelay());
        }

        IEnumerator Cor_StartIdleAfterInitialDelay()
        {
            float delaySeconds = GetInitialDelaySeconds();
            if (delaySeconds <= 0f)
            {
                StartIdle();
                yield break;
            }

            float remaining = delaySeconds;
            while (remaining > 0f)
            {
                if (onlyWhenVisible && !IsVisibleNow())
                {
                    yield return null;
                    continue;
                }

                float dt = useUnscaledTimeForInitialDelay ? Time.unscaledDeltaTime : Time.deltaTime;
                remaining -= Mathf.Max(0f, dt);
                yield return null;
            }

            StartIdle();
        }

        float GetInitialDelaySeconds()
        {
            if (!randomizeInitialDelay)
                return Mathf.Max(0f, initialDelaySeconds);

            float min = Mathf.Max(0f, randomInitialDelayRangeSeconds.x);
            float max = Mathf.Max(0f, randomInitialDelayRangeSeconds.y);
            if (max < min)
            {
                float tmp = min;
                min = max;
                max = tmp;
            }

            return (max <= min) ? min : Random.Range(min, max);
        }

        /// <summary>
        /// Kills active tweens safely.
        /// </summary>
        protected void KillIdle()
        {
            if (idleSeq != null) idleSeq.Kill();
            idleSeq = null;

            if (auxiliaryTween != null) auxiliaryTween.Kill();
            auxiliaryTween = null;
        }

        protected virtual void Update()
        {
            if (onlyWhenVisible && !IsVisibleNow())
            {
                PauseIdle();
                return;
            }

            if (HasExplicitExternalPauseOnUsedChannels())
            {
                PauseIdle();
                return;
            }

            if (autoPauseOnExternalTweens && IsExternallyAnimatingOnChannels(UsedChannels))
            {
                PauseIdle();
                isPausedByExternal = true;
                resumeTimer = resumeGraceTime;
                return;
            }

            if (isPausedByExternal)
            {
                if (resumeTimer > 0f) resumeTimer -= Time.deltaTime;
                else
                {
                    isPausedByExternal = false;
                    ResumeIdle();
                }
            }
        }

        protected void PauseIdle()
        {
            if (idleSeq != null) idleSeq.Pause();
            if (auxiliaryTween != null) auxiliaryTween.Pause();
        }

        protected void ResumeIdle()
        {
            if (idleSeq != null) idleSeq.Play();
            else BuildIdle();
        }

        // ---------- Visibility ----------
        /// <summary>
        /// Returns true if the object passes all visibility checks configured.
        /// </summary>
        protected bool IsVisibleNow()
        {
            if (!gameObject.activeInHierarchy) return false;
            if (optionalGraphic != null && !optionalGraphic.enabled) return false;

            if (respectAlphaVisibility)
            {
                float effAlpha = GetEffectiveAlpha();
                if (effAlpha < alphaThreshold) return false;
            }

            if (respectScaleVisibility)
            {
                float effScale = GetEffectiveScale01();
                if (effScale < minScaleThreshold) return false;
            }

            if (validateWithinCanvas && !IsInsideCanvasViewport(out _))
                return false;

            return true;
        }

        /// <summary>
        /// Effective alpha = product of parent CanvasGroups * Graphic alpha (if any).
        /// </summary>
        protected float GetEffectiveAlpha()
        {
            float canvasAlpha = 1f;
            Transform t = transform;
            while (t != null)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) canvasAlpha *= Mathf.Clamp01(cg.alpha);
                t = t.parent;
            }

            float graphicAlpha = 1f;
            if (optionalGraphic != null) graphicAlpha = optionalGraphic.color.a;
            else
            {
                var g = GetComponent<Graphic>();
                if (g != null) graphicAlpha = g.color.a;
            }

            return Mathf.Clamp01(canvasAlpha * graphicAlpha);
        }

        /// <summary>
        /// Mean of |localScale.x| and |localScale.y| for UI.
        /// </summary>
        protected float GetEffectiveScale01()
        {
            Vector3 ls = targetRect.localScale;
            float sx = Mathf.Abs(ls.x);
            float sy = Mathf.Abs(ls.y);
            return (sx + sy) * 0.5f;
        }

        /// <summary>
        /// Checks whether the RectTransform is inside canvas viewport. Also returns visible area ratio.
        /// </summary>
        protected bool IsInsideCanvasViewport(out float visibleAreaRatio)
        {
            visibleAreaRatio = 0f;
            if (targetRect == null) return false;
            var canvas = targetRect.GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            Vector3[] worldCorners = new Vector3[4];
            targetRect.GetWorldCorners(worldCorners);

            Camera cam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
                cam = canvas.worldCamera;

            Vector2[] screenPts = new Vector2[4];
            for (int i = 0; i < 4; i++)
                screenPts[i] = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);

            float minX = Mathf.Min(Mathf.Min(screenPts[0].x, screenPts[1].x), Mathf.Min(screenPts[2].x, screenPts[3].x));
            float maxX = Mathf.Max(Mathf.Max(screenPts[0].x, screenPts[1].x), Mathf.Max(screenPts[2].x, screenPts[3].x));
            float minY = Mathf.Min(Mathf.Min(screenPts[0].y, screenPts[1].y), Mathf.Min(screenPts[2].y, screenPts[3].y));
            float maxY = Mathf.Max(Mathf.Max(screenPts[0].y, screenPts[1].y), Mathf.Max(screenPts[2].y, screenPts[3].y));

            Rect uiRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            Rect canvasRect = canvas.pixelRect;

            Rect inter = RectIntersection(uiRect, canvasRect);
            if (inter.width <= 0f || inter.height <= 0f) return false;

            float uiArea = Mathf.Max(1f, uiRect.width * uiRect.height);
            float interArea = inter.width * inter.height;
            visibleAreaRatio = Mathf.Clamp01(interArea / uiArea);

            if (requireFullInside)
                return canvasRect.Contains(new Vector2(minX, minY)) && canvasRect.Contains(new Vector2(maxX, maxY));

            return visibleAreaRatio >= Mathf.Clamp01(minVisibleAreaRatio);
        }

        Rect RectIntersection(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin) return new Rect(0, 0, 0, 0);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // ---------- Conflict Detection ----------
        /// <summary>
        /// Returns true if any external tween is currently playing on conflicting channels.
        /// </summary>
        protected bool IsExternallyAnimatingOnChannels(UIAnimationChannels channelsToCheck)
        {
            if (channelsToCheck == UIAnimationChannels.None) return false;

            if (targetRect != null)
            {
                if (HasConflictingTweensForTarget(targetRect, channelsToCheck)) return true;
                if (HasConflictingTweensForTarget(targetRect.transform, channelsToCheck)) return true;
            }

            var g = optionalGraphic ? optionalGraphic : GetComponent<Graphic>();
            if (g != null)
            {
                if (HasConflictingTweensForTarget(g, channelsToCheck)) return true;
            }

            if ((channelsToCheck & UIAnimationChannels.Alpha) != 0)
            {
                Transform t = transform;
                while (t != null)
                {
                    var cg = t.GetComponent<CanvasGroup>();
                    if (cg != null && HasConflictingTweensForTarget(cg, UIAnimationChannels.Alpha)) return true;
                    t = t.parent;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if there are any active DOTween tweens on the given target that touch the provided channels.
        /// </summary>
        bool HasConflictingTweensForTarget(object target, UIAnimationChannels channelsToCheck)
        {
            List<Tween> tweens = DOTween.TweensByTarget(target, true);
            if (tweens == null || tweens.Count == 0) return false;

            for (int i = 0; i < tweens.Count; i++)
            {
                var tw = tweens[i];
                if (tw == null || !tw.active || !tw.IsPlaying()) continue;

                UIAnimationChannels twChannels = InferChannelsFromTween(tw, target);
                if ((twChannels & channelsToCheck) != 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Heuristically infers which idle channels a tween is affecting based on its generic value type and target.
        /// </summary>
        UIAnimationChannels InferChannelsFromTween(Tween tw, object target)
        {
            try
            {
                var t = tw.GetType();
                if (t.IsGenericType)
                {
                    var valueType = t.GetGenericArguments()[0];

                    if (valueType == typeof(Color)) return UIAnimationChannels.Alpha;
                    if (valueType == typeof(float)) return (target is CanvasGroup) ? UIAnimationChannels.Alpha : UIAnimationChannels.None;
                    if (valueType == typeof(Quaternion)) return UIAnimationChannels.Rotation;
                    if (valueType == typeof(Vector2)) return UIAnimationChannels.Position;

                    if (valueType == typeof(Vector3))
                    {
                        if (target is RectTransform || target is Transform)
                            return UIAnimationChannels.Position | UIAnimationChannels.Scale;
                        return UIAnimationChannels.None;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return UIAnimationChannels.None;
        }

        // ---------- Explicit Pause Coordination ----------
        protected bool HasExplicitExternalPauseOnUsedChannels()
        {
            UIAnimationChannels blockedChannels = UIAnimationChannels.None;
            foreach (var pair in externalPauseTokens)
                blockedChannels |= pair.Value;

            return (blockedChannels & UsedChannels) != 0;
        }

        public int AcquireExternalPause(UIAnimationChannels channels)
        {
            UIAnimationChannels overlappingChannels = channels & UsedChannels;
            if (overlappingChannels == UIAnimationChannels.None)
                return 0;

            int token = s_nextPauseToken++;
            externalPauseTokens[token] = overlappingChannels;
            PauseIdle();
            return token;
        }

        public void ReleaseExternalPause(int token)
        {
            if (token == 0)
                return;

            if (!externalPauseTokens.Remove(token))
                return;

            if (!HasExplicitExternalPauseOnUsedChannels() && enabled && gameObject.activeInHierarchy)
                ResumeIdle();
        }

        void ReleaseAllExternalPauseTokens()
        {
            externalPauseTokens.Clear();
        }

        static IdlePauseHandle CreatePauseHandle(List<IdlePauseEntry> entries)
        {
            return entries != null && entries.Count > 0 ? new IdlePauseHandle(entries) : IdlePauseHandle.Empty;
        }

        public static IdlePauseHandle PauseForTarget(RectTransform target, UIAnimationChannels channels)
        {
            if (target == null || channels == UIAnimationChannels.None)
                return IdlePauseHandle.Empty;

            if (!s_registeredByTarget.TryGetValue(target, out var idles) || idles.Count == 0)
                return IdlePauseHandle.Empty;

            var entries = new List<IdlePauseEntry>();
            foreach (var idle in idles)
            {
                if (idle == null)
                    continue;

                int token = idle.AcquireExternalPause(channels);
                if (token != 0)
                    entries.Add(new IdlePauseEntry(idle, token));
            }

            return CreatePauseHandle(entries);
        }

        internal readonly struct IdlePauseEntry
        {
            public readonly UI_IdleBase Idle;
            public readonly int Token;

            public IdlePauseEntry(UI_IdleBase idle, int token)
            {
                Idle = idle;
                Token = token;
            }
        }

        public sealed class IdlePauseHandle
        {
            public static readonly IdlePauseHandle Empty = new IdlePauseHandle(null);

            readonly List<IdlePauseEntry> entries;
            bool released;

            internal IdlePauseHandle(List<IdlePauseEntry> entries)
            {
                this.entries = entries;
            }

            public void Release(bool refreshBasePose = false)
            {
                if (released || entries == null)
                    return;

                released = true;

                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry.Idle != null)
                    {
                        if (refreshBasePose)
                            entry.Idle.RefreshBasePose();
                        entry.Idle.ReleaseExternalPause(entry.Token);
                    }
                }
            }
        }

        // ---------- Registry ----------
        void EnsureTargetAssigned()
        {
            if (targetRect == null)
                targetRect = GetComponent<RectTransform>();
        }

        void CacheBaseState()
        {
            if (targetRect == null)
                return;

            baseAnchoredPos = targetRect.anchoredPosition;
            baseScale = targetRect.localScale;
            baseZRotation = targetRect.localEulerAngles.z;
        }

        void RefreshTargetRegistration()
        {
            EnsureTargetAssigned();

            if (registeredTargetRect == targetRect)
                return;

            UnregisterTarget();

            if (targetRect == null)
                return;

            if (!s_registeredByTarget.TryGetValue(targetRect, out var idles))
            {
                idles = new HashSet<UI_IdleBase>();
                s_registeredByTarget[targetRect] = idles;
            }

            idles.Add(this);
            registeredTargetRect = targetRect;
        }

        void UnregisterTarget()
        {
            if (registeredTargetRect == null)
                return;

            if (s_registeredByTarget.TryGetValue(registeredTargetRect, out var idles))
            {
                idles.Remove(this);
                if (idles.Count == 0)
                    s_registeredByTarget.Remove(registeredTargetRect);
            }

            registeredTargetRect = null;
        }

        // ---------- Gizmos ----------
        protected virtual void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            var rt = targetRect ? targetRect : GetComponent<RectTransform>();
            if (rt == null) return;

            DrawBehaviorGizmos(rt);
        }
    }
}
