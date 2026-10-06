using DG.Tweening;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Iung.Animation
{
    /// <summary>
    /// Represents a bidirectional UI animation unit that supports both Show and Hide flows
    /// using the same configuration. A single AnimationProperty instance is responsible
    /// for defining the initial (hidden) state and the target (revealed) state.
    ///
    /// The state configured through ConfigureStartPosition() or ForceStart() is treated
    /// as the "Hide" state, while calling Show() transitions the element to its final
    /// configured state. Calling Hide() returns the element back to its initial state.
    ///
    /// It is not necessary to maintain separate lists for Show and Hide animations.
    /// The same list of AnimationProperty objects should be reused for both directions.
    ///
    /// Whenever possible, it is recommended to compose UI animations using lists of
    /// AnimationProperty and trigger them through AnimationPropertyController in order
    /// to ensure better readability, architectural consistency, and future flexibility.
    /// </summary>
    [System.Serializable]
    public class AnimationProperty : ISerializationCallbackReceiver
    {
        // Internal computed destinations (legacy flow)
        float endDestination;
        Vector3 startDestination;

        // Cached "revealed" scale (original state before being hidden).
        // This preserves negative axes (e.g., -1,1,1 for horizontal flip).
        Vector3 revealedScale;
        bool hasRevealedScale;

        /// <summary>
        /// Available animation types handled by this property.
        /// </summary>
        public enum AnimationType
        {
            Scale,
            Fade,
            OutsideScreen_Left,
            OutsideScreen_Right,
            OutsideScreen_Up,
            OutsideScreen_Down,
            Text_Char,
            RectTween
        }

        public static bool IsOutsideScreenAnimation(AnimationType animationType)
        {
            return animationType == AnimationType.OutsideScreen_Left ||
                   animationType == AnimationType.OutsideScreen_Right ||
                   animationType == AnimationType.OutsideScreen_Up ||
                   animationType == AnimationType.OutsideScreen_Down;
        }

        public static Vector2 CalculateLegacyHideAnchoredPosition(AnimationType animationType, RectTransform rectTransform, Vector2 viewportSize)
        {
            Vector2 destination = rectTransform.anchoredPosition;
            Rect objectRect = rectTransform.rect;
            Vector2 anchorMax = rectTransform.anchorMax;
            Vector2 anchorMin = rectTransform.anchorMin;

            float screenPosition;
            float anchoredSize;

            switch (animationType)
            {
                case AnimationType.OutsideScreen_Left:
                    screenPosition = -viewportSize.x * Mathf.Abs(anchorMin.x + anchorMax.x);
                    anchoredSize = objectRect.width;
                    destination.x = screenPosition - anchoredSize;
                    break;

                case AnimationType.OutsideScreen_Right:
                    screenPosition = viewportSize.x * (2 - Mathf.Abs(anchorMin.x + anchorMax.x));
                    anchoredSize = objectRect.width;
                    destination.x = screenPosition + anchoredSize;
                    break;

                case AnimationType.OutsideScreen_Up:
                    screenPosition = viewportSize.y * (2 - Mathf.Abs(anchorMin.y + anchorMax.y));
                    anchoredSize = objectRect.height;
                    destination.y = screenPosition + anchoredSize;
                    break;

                case AnimationType.OutsideScreen_Down:
                    screenPosition = -viewportSize.y * Mathf.Abs(anchorMin.y + anchorMax.y);
                    anchoredSize = objectRect.height;
                    destination.y = screenPosition - anchoredSize;
                    break;
            }

            return destination;
        }

        [Flags]
        public enum IdleSyncOptions
        {
            None = 0,
            RecalculateOnShow = 1 << 0,
            RecalculateOnHide = 1 << 1,
            ResumeIdleAfterHide = 1 << 2
        }

        [Tooltip("Target GameObject for this animation (UI element)")]
        public GameObject _object;

        [Tooltip("Duration of the Show animation")]
        public float time = 0.3f;

        [Tooltip("Duration of the Hide animation")]
        public float hideTime = 0.3f;

        [Tooltip("Delay before Show starts")]
        public float delay;

        [Tooltip("Delay before Hide starts")]
        public float delayHide;

        [Tooltip("Ease for Show")]
        public Ease _easeIn = Ease.OutQuint;

        [Tooltip("Ease for Hide")]
        public Ease _easeOut = Ease.InQuint;

        [Tooltip("Animation type for this property")]
        public AnimationType _animationType;

        [SerializeField, Tooltip("Controls how this animation coordinates idle state. Options can be combined.")]
        IdleSyncOptions idleSyncOptions = IdleSyncOptions.RecalculateOnShow;

        Tween _animation;
        UI_IdleBase.IdlePauseHandle idlePauseHandle;

        [SerializeField] bool _serialized = false;
        [SerializeField] bool _idleSyncOptionsInitialized = false;

        /// <summary>True if object is currently fully hidden.</summary>
        public bool isHided;
        /// <summary>True if object is currently fully revealed.</summary>
        public bool isRevealed;
        /// <summary>True while any tween is running.</summary>
        public bool isAnimating;

        bool rayscastVisible;

        #region Manual Range (optional)
        /// <summary>
        /// Enables manual Start/End values for supported types (Scale, Fade, OutsideScreen_*).
        /// When disabled, the default behavior from the legacy flow is used.
        /// </summary>
        [SerializeField, Tooltip("Enable manual Start/End values (Scale, Fade, OutsideScreen_* only)")]
        bool useManualRange = false;

        /// <summary>Manual START value for Scale/Fade.</summary>
        [SerializeField, Tooltip("Manual START value for Scale/Fade (ignored if toggle is OFF)")]
        float manualStartFloat;

        /// <summary>Manual END value for Scale/Fade.</summary>
        [SerializeField, Tooltip("Manual END value for Scale/Fade (ignored if toggle is OFF)")]
        float manualEndFloat;

        /// <summary>Manual START anchoredPosition for OutsideScreen_* (x,y).</summary>
        [SerializeField, Tooltip("Manual START anchoredPosition (OutsideScreen_* only)")]
        Vector2 manualStartPos;

        /// <summary>Manual END anchoredPosition for OutsideScreen_* (x,y).</summary>
        [SerializeField, Tooltip("Manual END anchoredPosition (OutsideScreen_* only)")]
        Vector2 manualEndPos;

        /// <summary>
        /// Returns true if current type supports manual range fields.
        /// </summary>
        bool TypeSupportsManualRange =>
            _animationType == AnimationType.Scale ||
            _animationType == AnimationType.Fade ||
            _animationType == AnimationType.OutsideScreen_Left ||
            _animationType == AnimationType.OutsideScreen_Right ||
            _animationType == AnimationType.OutsideScreen_Up ||
            _animationType == AnimationType.OutsideScreen_Down;
        #endregion

        #region Prototype Compatibility API
        public AnimationType AnimationMode
        {
            get => _animationType;
            set => _animationType = value;
        }

        public float ShowDuration
        {
            get => Mathf.Max(0f, time);
            set => time = Mathf.Max(0f, value);
        }

        public float HideDuration
        {
            get => Mathf.Max(0f, hideTime > 0f ? hideTime : time);
            set => hideTime = Mathf.Max(0f, value);
        }

        public Ease EaseIn
        {
            get => _easeIn;
            set => _easeIn = value;
        }

        public Ease EaseOut
        {
            get => _easeOut;
            set => _easeOut = value;
        }

        public bool UseManualRange
        {
            get => useManualRange;
            set => useManualRange = value;
        }

        public float ManualStartFloat
        {
            get => manualStartFloat;
            set => manualStartFloat = value;
        }

        public float ManualEndFloat
        {
            get => manualEndFloat;
            set => manualEndFloat = value;
        }

        public Vector2 ManualStartPos
        {
            get => manualStartPos;
            set => manualStartPos = value;
        }

        public Vector2 ManualEndPos
        {
            get => manualEndPos;
            set => manualEndPos = value;
        }

        public string BuildDisplayName()
        {
            string objectName = _object != null ? _object.name : string.Empty;
            string typeName = _animationType.ToString();
            return string.IsNullOrEmpty(objectName) ? typeName : $"{objectName} - {typeName}";
        }
        #endregion

        /// <summary>
        /// Default constructor sets sensible defaults.
        /// </summary>
        public AnimationProperty()
        {
            time = 0.3f;
            hideTime = 0.3f;
            _easeIn = Ease.OutQuint;
            _easeOut = Ease.InQuint;
        }

        /// <summary>
        /// Prepares the object to its START state before Show (and stores final destinations).
        /// If manual range is enabled for supported types, applies manual START instead.
        /// </summary>
        public void ConfigureStartPosition()
        {
            if (_object == null)
                return;

            CacheRevealedScaleIfNeeded();

            // Manual override for supported types
            if (useManualRange && TypeSupportsManualRange)
            {
                ApplyManualStartImmediate();
                CacheLegacyEndIfNeeded(); // keep legacy caches valid when possible
                return;
            }

            // Legacy flow (unchanged)
            Vector3 destination = _object.GetComponent<RectTransform>().anchoredPosition;
            Rect _objectRect = _object.GetComponent<RectTransform>().rect;

            Vector2 anchorMax = _object.GetComponent<RectTransform>().anchorMax;
            Vector2 anchorMin = _object.GetComponent<RectTransform>().anchorMin;

            float screenPosition;
            float anchoredSize;

            switch (_animationType)
            {
                case AnimationType.Scale:
                    // Keep legacy cache for backwards compatibility, but do NOT assume Vector3.one.
                    // Revealing should restore the original scale, including negative axes.
                    endDestination = hasRevealedScale ? revealedScale.x : _object.transform.localScale.x;
                    _object.transform.localScale = Vector3.zero;
                    break;

                case AnimationType.Fade:
                    if (_object.GetComponent<MaskableGraphic>() == null)
                        Debug.LogError("Don't have graphic image");
                    else
                    {
                        startDestination.x = 0;
                        var graphic = _object.GetComponent<MaskableGraphic>();
                        endDestination = graphic.color.a;
                        var color = graphic.color;
                        color.a = 0f;
                        graphic.color = color;
                        rayscastVisible = graphic.raycastTarget;
                        graphic.raycastTarget = false;
                    }
                    break;

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    destination = CalculateLegacyHideAnchoredPosition(
                        _animationType,
                        _object.GetComponent<RectTransform>(),
                        new Vector2(Screen.width, Screen.height));

                    if (_animationType == AnimationType.OutsideScreen_Left || _animationType == AnimationType.OutsideScreen_Right)
                        endDestination = _object.GetComponent<RectTransform>().anchoredPosition.x;
                    else
                        endDestination = _object.GetComponent<RectTransform>().anchoredPosition.y;
                    break;

                case AnimationType.Text_Char:
                    {
                        var tmp = _object.GetComponent<TextMeshProUGUI>();
                        if (tmp == null)
                        {
                            Debug.LogError("DON'T HAVE TEXTMESHPRO");
                        }
                        else
                        {
                            tmp.ForceMeshUpdate(true, true);
                            tmp.maxVisibleCharacters = 0;
                        }
                        break;
                    }

                case AnimationType.RectTween:
                    if (_object.GetComponent<UI_RectTransformTweener>() == null)
                    {
                        Debug.LogError("[AnimationProperty] Missing UI_RectTransformTweener component for RectTween type.");
                    }
                    else
                    {
                        _object.GetComponent<UI_RectTransformTweener>().ForceState(_object.GetComponent<UI_RectTransformTweener>().stateA);
                    }
                    break;
            }

            if (_animationType != AnimationType.Fade)
            {
                _object.GetComponent<RectTransform>().anchoredPosition = destination;
                startDestination = destination;
            }
        }

        /// <summary>
        /// Forces the object to START state immediately (without tween).
        /// Respects manual range when enabled and supported.
        /// </summary>
        public void ForceStart()
        {
            if (_object == null)
                return;

            CacheRevealedScaleIfNeeded();

            RectTransform _rectObject = _object.GetComponent<RectTransform>();

            // Manual override for supported types
            if (useManualRange && TypeSupportsManualRange)
            {
                ApplyManualStartImmediate();
                return;
            }

            // Legacy behavior
            switch (_animationType)
            {
                case AnimationType.Scale:
                    _object.transform.localScale = Vector3.zero;
                    break;

                case AnimationType.Fade:
                    if (_object.GetComponent<MaskableGraphic>() == null)
                        Debug.LogError("Don't have Graphic image");
                    else
                    {
                        var graphic = _object.GetComponent<MaskableGraphic>();
                        var color = graphic.color;
                        color.a = startDestination.x;
                        graphic.color = color;
                        graphic.raycastTarget = false;
                    }
                    break;

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                    _rectObject.anchoredPosition = new Vector2(startDestination.x, _rectObject.anchoredPosition.y);
                    break;

                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    _rectObject.anchoredPosition = new Vector2(_rectObject.anchoredPosition.x, startDestination.y);
                    break;

                case AnimationType.Text_Char:
                    {
                        var tmp = _object.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.ForceMeshUpdate(true, true);
                            tmp.maxVisibleCharacters = 0;
                        }
                        break;
                    }

                case AnimationType.RectTween:
                    {
                        var tweener = _object.GetComponent<UI_RectTransformTweener>();
                        if (tweener != null)
                            tweener.ForceState(tweener.stateA);
                        else
                            Debug.LogWarning("[AnimationProperty] RectTween ForceStart called but UI_RectTransformTweener is missing.");
                        break;
                    }
            }
        }

        /// <summary>
        /// Forces the object to END state immediately (without tween).
        /// Respects manual range when enabled and supported.
        /// </summary>
        public void ForceEnd()
        {
            if (_object == null)
                return;

            CacheRevealedScaleIfNeeded();

            RectTransform _rectObject = _object.GetComponent<RectTransform>();

            // Manual override for supported types
            if (useManualRange && TypeSupportsManualRange)
            {
                ApplyManualEndImmediate();
                return;
            }

            // Legacy behavior
            switch (_animationType)
            {
                case AnimationType.Scale:
                    // Force the revealed/original scale instead of forcing Vector3.one.
                    _object.transform.localScale = hasRevealedScale ? revealedScale : Vector3.one;
                    break;

                case AnimationType.Fade:
                    if (_object.GetComponent<MaskableGraphic>() == null)
                        Debug.LogError("Don't have Graphic image");
                    else
                    {
                        var graphic = _object.GetComponent<MaskableGraphic>();
                        var color = graphic.color;
                        color.a = endDestination;
                        graphic.color = color;
                        graphic.raycastTarget = rayscastVisible;
                    }
                    break;

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                    _rectObject.anchoredPosition = new Vector2(endDestination, _rectObject.anchoredPosition.y);
                    break;

                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    _rectObject.anchoredPosition = new Vector2(_rectObject.anchoredPosition.x, endDestination);
                    break;

                case AnimationType.Text_Char:
                    {
                        var tmp = _object.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.ForceMeshUpdate(true, true);
                            tmp.maxVisibleCharacters = tmp.textInfo.characterCount;
                        }
                        break;
                    }

                case AnimationType.RectTween:
                    {
                        var tweener = _object.GetComponent<UI_RectTransformTweener>();
                        if (tweener != null)
                            tweener.ForceState(tweener.stateB);
                        else
                            Debug.LogWarning("[AnimationProperty] RectTween ForceEnd called but UI_RectTransformTweener is missing.");
                        break;
                    }
            }
        }

        /// <summary>
        /// Plays the Show animation (START -> END). Respects manual range when enabled and supported.
        /// </summary>
        public void Show(Action OnFinish = null)
        {
            if (_object == null)
            {
                OnFinish?.Invoke();
                return;
            }

            CacheRevealedScaleIfNeeded();

            RectTransform _rectObject = _object.GetComponent<RectTransform>();
            isHided = false;
            isAnimating = true;

            _animation?.Kill();
            AcquireIdlePause();

            if (_rectObject.GetComponent<UI_ButtonAnimation>() != null)
            {
                _rectObject.GetComponent<UI_ButtonAnimation>().block = true;
                OnFinish += () =>
                {
                    _rectObject.GetComponent<UI_ButtonAnimation>().block = false;
                };
            }

            // Manual override for supported types
            if (useManualRange && TypeSupportsManualRange)
            {
                switch (_animationType)
                {
                    case AnimationType.Scale:
                        _animation = TrackAnimationTween(_object.transform
                            .DOScale(BuildSignedUniformScale(manualEndFloat), time)
                            .SetDelay(delay)
                            .SetEase(_easeIn)
                            .OnComplete(() => CompleteReveal(OnFinish)));
                        break;

                    case AnimationType.Fade:
                        {
                            var g = _object.GetComponent<MaskableGraphic>();
                            if (g == null) { Debug.LogError("Don't have Graphic image"); break; }
                            rayscastVisible = g.raycastTarget;
                            g.raycastTarget = false;
                            _animation = TrackAnimationTween(g.DOFade(manualEndFloat, time)
                                .SetDelay(delay)
                                .SetEase(_easeIn)
                                .OnComplete(() =>
                                {
                                    isRevealed = true;
                                    isAnimating = false;
                                    ReleaseIdlePause(refreshBasePose: HasIdleSyncOption(IdleSyncOptions.RecalculateOnShow));
                                    g.raycastTarget = rayscastVisible;
                                    OnFinish?.Invoke();
                                }));
                            break;
                        }

                    case AnimationType.OutsideScreen_Left:
                    case AnimationType.OutsideScreen_Right:
                    case AnimationType.OutsideScreen_Up:
                    case AnimationType.OutsideScreen_Down:
                        _animation = TrackAnimationTween(_rectObject
                            .DOAnchorPos(manualEndPos, time)
                            .SetDelay(delay)
                            .SetEase(_easeIn)
                            .OnComplete(() => CompleteReveal(OnFinish)));
                        break;
                }
                return;
            }

            // Legacy Show
            switch (_animationType)
            {
                case AnimationType.Scale:
                    _animation = TrackAnimationTween(_object.transform
                        .DOScale(hasRevealedScale ? revealedScale : Vector3.one, time)
                        .SetDelay(delay)
                        .SetEase(_easeIn)
                        .OnComplete(() => CompleteReveal(OnFinish)));
                    break;

                case AnimationType.Fade:
                    if (_object.GetComponent<MaskableGraphic>() == null)
                        Debug.LogError("Don't have Graphic image");
                    else
                    {
                        _animation = TrackAnimationTween(_object.GetComponent<MaskableGraphic>().DOFade(endDestination, time)
                            .SetDelay(delay)
                            .SetEase(_easeIn)
                            .OnComplete(() =>
                            {
                                isRevealed = true;
                                isAnimating = false;
                                ReleaseIdlePause(refreshBasePose: HasIdleSyncOption(IdleSyncOptions.RecalculateOnShow));
                                _object.GetComponent<MaskableGraphic>().raycastTarget = rayscastVisible;
                                OnFinish?.Invoke();
                            }));
                    }
                    break;

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                    _animation = TrackAnimationTween(_rectObject.DOAnchorPosX(endDestination, time)
                        .SetDelay(delay)
                        .SetEase(_easeIn)
                        .OnComplete(() => CompleteReveal(OnFinish)));
                    break;

                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    _animation = TrackAnimationTween(_rectObject.DOAnchorPosY(endDestination, time)
                        .SetDelay(delay)
                        .SetEase(_easeIn)
                        .OnComplete(() => CompleteReveal(OnFinish)));
                    break;

                case AnimationType.Text_Char:
                    {
                        var tmp = _object.GetComponent<TextMeshProUGUI>();
                        if (tmp == null) break;

                        tmp.maxVisibleCharacters = 0;
                        tmp.ForceMeshUpdate(true, true);
                        int totalCharacters = tmp.textInfo.characterCount;

                        _animation = TrackAnimationTween(DOTween
                            .To(() => tmp.maxVisibleCharacters, x => tmp.maxVisibleCharacters = x, totalCharacters, time)
                            .SetEase(_easeIn)
                            .SetDelay(delay)
                            .OnComplete(() => CompleteReveal(OnFinish)));
                        break;
                    }

                case AnimationType.RectTween:
                    if (_object.GetComponent<UI_RectTransformTweener>() != null)
                    {
                        bool rectTweenStarted = false;
                        _animation = DOVirtual.DelayedCall(delay, () =>
                        {
                            rectTweenStarted = true;
                            _object.GetComponent<UI_RectTransformTweener>().easeType = _easeIn;
                            _object.GetComponent<UI_RectTransformTweener>().AnimateTowards(_object.GetComponent<UI_RectTransformTweener>().stateB, () =>
                            {
                                CompleteReveal(OnFinish);
                            });
                        }).OnKill(() =>
                        {
                            if (!rectTweenStarted && isAnimating)
                            {
                                isAnimating = false;
                                ReleaseIdlePause();
                            }
                        });
                    }
                    break;
            }
        }

        /// <summary>
        /// Plays the Hide animation (END -> START). Respects manual range when enabled and supported.
        /// </summary>
        public void Hide(Action OnFinish = null)
        {
            if (_object == null)
            {
                OnFinish?.Invoke();
                return;
            }

            CacheRevealedScaleIfNeeded();

            RectTransform _rectObject = _object.GetComponent<RectTransform>();

            isRevealed = false;
            isAnimating = true;
            _animation?.Kill();
            AcquireIdlePause();

            if (_rectObject.GetComponent<UI_ButtonAnimation>() != null)
            {
                _rectObject.GetComponent<UI_ButtonAnimation>().block = true;
                OnFinish += () =>
                {
                    _rectObject.GetComponent<UI_ButtonAnimation>().block = false;
                };
            }

            // Manual override for supported types
            if (useManualRange && TypeSupportsManualRange)
            {
                switch (_animationType)
                {
                    case AnimationType.Scale:
                        _animation = TrackAnimationTween(_object.transform
                            .DOScale(BuildSignedUniformScale(manualStartFloat), HideDuration)
                            .SetDelay(delayHide)
                            .SetEase(_easeOut)
                            .OnComplete(() => CompleteHide(OnFinish)));
                        break;

                    case AnimationType.Fade:
                        {
                            var g = _object.GetComponent<MaskableGraphic>();
                            if (g == null) { Debug.LogError("Don't have graphic image"); break; }
                            _animation = TrackAnimationTween(g.DOFade(manualStartFloat, HideDuration)
                                .SetDelay(delayHide)
                                .SetEase(_easeOut)
                                .OnComplete(() =>
                                {
                                    CompleteHide(OnFinish);
                                    g.raycastTarget = false;
                                }));
                            break;
                        }

                    case AnimationType.OutsideScreen_Left:
                    case AnimationType.OutsideScreen_Right:
                    case AnimationType.OutsideScreen_Up:
                    case AnimationType.OutsideScreen_Down:
                        _animation = TrackAnimationTween(_rectObject
                            .DOAnchorPos(manualStartPos, HideDuration)
                            .SetDelay(delayHide)
                            .SetEase(_easeOut)
                            .OnComplete(() => CompleteHide(OnFinish)));
                        break;
                }
                return;
            }

            // Legacy Hide
            switch (_animationType)
            {
                case AnimationType.Scale:
                    _animation = TrackAnimationTween(_object.transform.DOScale(0, HideDuration)
                        .SetDelay(delayHide)
                        .SetEase(_easeOut)
                        .OnComplete(() => CompleteHide(OnFinish)));
                    break;

                case AnimationType.Fade:
                    if (_object.GetComponent<MaskableGraphic>() == null)
                        Debug.LogError("Don't have graphic image");
                    else
                    {
                        _animation = TrackAnimationTween(_object.GetComponent<MaskableGraphic>().DOFade(startDestination.x, HideDuration)
                            .SetDelay(delayHide)
                            .SetEase(_easeOut)
                            .OnComplete(() =>
                            {
                                CompleteHide(OnFinish);
                                _object.GetComponent<MaskableGraphic>().raycastTarget = false;
                            }));
                    }
                    break;

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                    _animation = TrackAnimationTween(_rectObject.DOAnchorPosX(startDestination.x, HideDuration)
                        .SetDelay(delayHide)
                        .SetEase(_easeOut)
                        .OnComplete(() => CompleteHide(OnFinish)));
                    break;

                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    _animation = TrackAnimationTween(_rectObject.DOAnchorPosY(startDestination.y, HideDuration)
                        .SetDelay(delayHide)
                        .SetEase(_easeOut)
                        .OnComplete(() => CompleteHide(OnFinish)));
                    break;

                case AnimationType.Text_Char:
                    {
                        var tmp = _object.GetComponent<TextMeshProUGUI>();
                        if (tmp == null)
                        {
                            CompleteHide(OnFinish);
                            break;
                        }

                        tmp.ForceMeshUpdate(true, true);
                        int current = tmp.maxVisibleCharacters;
                        _animation = TrackAnimationTween(DOTween
                            .To(() => current, x => { current = x; tmp.maxVisibleCharacters = x; }, 0, HideDuration)
                            .SetEase(_easeOut)
                            .SetDelay(delayHide)
                            .OnComplete(() => CompleteHide(OnFinish)));
                        break;
                    }

                case AnimationType.RectTween:
                    if (_object.GetComponent<UI_RectTransformTweener>() != null)
                    {
                        bool rectTweenStarted = false;
                        _animation = DOVirtual.DelayedCall(delayHide, () =>
                        {
                            rectTweenStarted = true;
                            _object.GetComponent<UI_RectTransformTweener>().easeType = _easeOut;
                            _object.GetComponent<UI_RectTransformTweener>().AnimateTowards(_object.GetComponent<UI_RectTransformTweener>().stateA, () =>
                            {
                                CompleteHide(OnFinish);
                            });
                        }).OnKill(() =>
                        {
                            if (!rectTweenStarted && isAnimating)
                            {
                                isAnimating = false;
                                ReleaseIdlePause();
                            }
                        });
                    }
                    break;
            }
        }

        /// <summary>
        /// Coroutine guard to kill the tween if the object gets destroyed/disabled mid-animation.
        /// </summary>
        IEnumerator Cor_CheckIfObjectIsDisabledOrDestroyed()
        {
            while (isAnimating && _object != null && _object.activeInHierarchy)
            {
                yield return null;
            }
            _animation?.Kill();
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (!_serialized)
            {
                _serialized = true;
                time = 0.3f;
                hideTime = 0.3f;
                _easeIn = Ease.OutQuint;
                _easeOut = Ease.InQuint;
            }

            if (!_idleSyncOptionsInitialized)
            {
                idleSyncOptions = IdleSyncOptions.RecalculateOnShow;
                _idleSyncOptionsInitialized = true;
            }
        }

        #region Helpers
        /// <summary>Applies manual START immediately for supported types.</summary>
        void ApplyManualStartImmediate()
        {
            var rect = _object.GetComponent<RectTransform>();

            switch (_animationType)
            {
                case AnimationType.Scale:
                    CacheRevealedScaleIfNeeded();
                    _object.transform.localScale = BuildSignedUniformScale(manualStartFloat);
                    break;

                case AnimationType.Fade:
                    {
                        var g = _object.GetComponent<MaskableGraphic>();
                        if (g == null) { Debug.LogError("Don't have Graphic image"); break; }
                        var c = g.color; c.a = manualStartFloat;
                        g.color = c;
                        g.raycastTarget = false;
                        break;
                    }

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    if (rect != null) rect.anchoredPosition = manualStartPos;
                    break;
            }
        }

        /// <summary>Applies manual END immediately for supported types.</summary>
        void ApplyManualEndImmediate()
        {
            var rect = _object.GetComponent<RectTransform>();

            switch (_animationType)
            {
                case AnimationType.Scale:
                    CacheRevealedScaleIfNeeded();
                    _object.transform.localScale = BuildSignedUniformScale(manualEndFloat);
                    break;

                case AnimationType.Fade:
                    {
                        var g = _object.GetComponent<MaskableGraphic>();
                        if (g == null) { Debug.LogError("Don't have Graphic image"); break; }
                        var c = g.color; c.a = manualEndFloat;
                        g.color = c;
                        g.raycastTarget = rayscastVisible;
                        break;
                    }

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    if (rect != null) rect.anchoredPosition = manualEndPos;
                    break;
            }
        }

        /// <summary>Completes Show bookkeeping.</summary>
        void CompleteReveal(Action onFinish)
        {
            isRevealed = true;
            isAnimating = false;
            ReleaseIdlePause(refreshBasePose: HasIdleSyncOption(IdleSyncOptions.RecalculateOnShow));
            onFinish?.Invoke();
        }

        /// <summary>Completes Hide bookkeeping.</summary>
        void CompleteHide(Action onFinish)
        {
            isHided = true;
            isAnimating = false;

            if (HasIdleSyncOption(IdleSyncOptions.ResumeIdleAfterHide))
                ReleaseIdlePause(refreshBasePose: HasIdleSyncOption(IdleSyncOptions.RecalculateOnHide));

            onFinish?.Invoke();
        }

        void AcquireIdlePause()
        {
            ReleaseIdlePause();

            if (_object == null)
                return;

            RectTransform rect = _object.GetComponent<RectTransform>();
            idlePauseHandle = UI_IdleBase.PauseForTarget(rect, GetAffectedChannels());
        }

        void ReleaseIdlePause(bool refreshBasePose = false)
        {
            idlePauseHandle?.Release(refreshBasePose);
            idlePauseHandle = null;
        }

        UIAnimationChannels GetAffectedChannels()
        {
            switch (_animationType)
            {
                case AnimationType.Scale:
                    return UIAnimationChannels.Scale;

                case AnimationType.Fade:
                    return UIAnimationChannels.Alpha;

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    return UIAnimationChannels.Position;

                case AnimationType.RectTween:
                    return UIAnimationChannels.Position | UIAnimationChannels.Scale | UIAnimationChannels.Rotation;

                default:
                    return UIAnimationChannels.None;
            }
        }

        bool HasIdleSyncOption(IdleSyncOptions option)
        {
            return (idleSyncOptions & option) != 0;
        }

        Tween TrackAnimationTween(Tween tween)
        {
            if (tween == null)
                return null;

            return tween.OnKill(() =>
            {
                if (!isAnimating)
                    return;

                isAnimating = false;
                ReleaseIdlePause();
            });
        }

        /// <summary>
        /// Keeps legacy caches valid if needed, when manual range is active (to avoid null paths elsewhere).
        /// </summary>
        void CacheLegacyEndIfNeeded()
        {
            if (_object == null) return;
            var rect = _object.GetComponent<RectTransform>();

            switch (_animationType)
            {
                case AnimationType.Scale:
                    endDestination = manualEndFloat;
                    startDestination = _object.transform.localScale;
                    break;

                case AnimationType.Fade:
                    endDestination = manualEndFloat;
                    startDestination.x = manualStartFloat; // legacy alpha cache
                    break;

                case AnimationType.OutsideScreen_Left:
                case AnimationType.OutsideScreen_Right:
                    if (rect != null)
                    {
                        startDestination = new Vector3(manualStartPos.x, manualStartPos.y, 0);
                        endDestination = manualEndPos.x;
                    }
                    break;

                case AnimationType.OutsideScreen_Up:
                case AnimationType.OutsideScreen_Down:
                    if (rect != null)
                    {
                        startDestination = new Vector3(manualStartPos.x, manualStartPos.y, 0);
                        endDestination = manualEndPos.y;
                    }
                    break;
            }
        }

        void CacheRevealedScaleIfNeeded()
        {
            if (hasRevealedScale) return;
            if (_object == null) return;

            revealedScale = _object.transform.localScale;
            hasRevealedScale = true;
        }

        Vector3 BuildSignedUniformScale(float uniform)
        {
            if (Mathf.Approximately(uniform, 0f))
                return Vector3.zero;

            if (!hasRevealedScale)
                return new Vector3(uniform, uniform, uniform);

            float x = Mathf.Sign(revealedScale.x) * uniform;
            float y = Mathf.Sign(revealedScale.y) * uniform;
            float z = Mathf.Sign(revealedScale.z) * uniform;
            return new Vector3(x, y, z);
        }
        #endregion
    }
}
