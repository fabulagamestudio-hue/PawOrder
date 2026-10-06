using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Iung.Animation
{
    /// <summary>
    /// Controls UI screens composed of AnimationProperty groups.
    /// Each screen can auto-advance after a custom interval or wait for a manual trigger (button).
    /// On subsequent scene loads (not the very first time the app sees this controller),
    /// it snaps all child Animators to the end of their current state and calls PlayFirstScreen().
    /// </summary>
    public class UI_ScreenSequenceController : MonoBehaviour
    {
        /// <summary>
        /// Determines how a screen advances to the next one.
        /// </summary>
        public enum ScreenAdvanceMode
        {
            /// <summary>Automatically advance after a time interval.</summary>
            Timed,
            /// <summary>Wait for an external trigger (Continue()) to advance.</summary>
            Manual
        }

        [Serializable]
        public class ScreenGroup
        {
            /// <summary>
            /// Logical name for organization in the Inspector only.
            /// </summary>
            [SerializeField, Tooltip("Logical name for organization in the Inspector.")]
            private string name;

            /// <summary>
            /// Animations that compose this screen. They will Reveal/Hide together.
            /// </summary>
            [SerializeField, Tooltip("All AnimationProperty entries that compose this screen.")]
            private List<AnimationProperty> animations = new List<AnimationProperty>();

            /// <summary>
            /// How this screen advances to the next one (Timed or Manual).
            /// </summary>
            [SerializeField, Tooltip("How this screen advances to the next one (Timed or Manual).")]
            private ScreenAdvanceMode advanceMode = ScreenAdvanceMode.Timed;

            /// <summary>
            /// If Timed, how many seconds to wait before advancing. Ignored in Manual.
            /// </summary>
            [SerializeField, Tooltip("If Timed, how many seconds to wait before advancing. Ignored in Manual.")]
            private float customIntervalSeconds = 5f;

            /// <summary>Exposes the internal list.</summary>
            public List<AnimationProperty> Animations => animations;

            /// <summary>Exposes the selected advance mode.</summary>
            public ScreenAdvanceMode AdvanceMode => advanceMode;

            /// <summary>Exposes the custom interval in seconds.</summary>
            public float Interval => customIntervalSeconds;

            /// <summary>
            /// Invoked when this screen finishes its Reveal animation.
            /// </summary>
            [SerializeField, Tooltip("Invoked when this screen finishes its Reveal animation.")]
            private UnityEvent onShown = new UnityEvent();

            /// <summary>
            /// Invoked when this screen finishes its Hide animation.
            /// </summary>
            [SerializeField, Tooltip("Invoked when this screen finishes its Hide animation.")]
            private UnityEvent onHidden = new UnityEvent();

            /// <summary>Read-only accessor for onShown.</summary>
            public UnityEvent OnShown => onShown;

            /// <summary>Read-only accessor for onHidden.</summary>
            public UnityEvent OnHidden => onHidden;
        }

        [Header("Screens")]
        [SerializeField, Tooltip("Ordered list of screens. Each screen is a group of animations that Reveal/Hide together.")]
        private List<ScreenGroup> screens = new List<ScreenGroup>();

        [Header("Start")]
        [SerializeField, Tooltip("Index of the screen shown on Start (0-based).")]
        private int startIndex = 0;

        [Header("Loop")]
        [SerializeField, Tooltip("If true, wraps from last to first when advancing past the end.")]
        private bool loop = true;

        [Header("Input (New Input System)")]
        [SerializeField, Tooltip("Optional input action to go to the next screen (performed triggers NextScreen).")]
        private InputActionReference nextAction;

        [SerializeField, Tooltip("Optional input action to go to the previous screen (performed triggers PrevScreen).")]
        private InputActionReference prevAction;

        [Header("Events")]
        [SerializeField, Tooltip("Invoked after a screen successfully becomes visible. Provides the new screen index.")]
        private UnityEvent<int> onScreenChanged;

        [SerializeField, Tooltip("Invoked when a full cycle completes (e.g., last->first if loop is enabled).")]
        private UnityEvent onCycleCompleted;

        [SerializeField, Tooltip("Invoked whenever the controller reaches a Manual screen and is waiting for Continue().")]
        private UnityEvent<int> onWaitingManual;

        [SerializeField, Tooltip("Gizmos: draw current index near this object in scene view.")]
        private bool drawGizmos = true;

        /// <summary>Current screen index.</summary>
        public int CurrentIndex => currentIndex;

        /// <summary>Is there an ongoing transition?</summary>
        public bool IsTransitioning => isTransitioning;

        int currentIndex = -1;
        bool isTransitioning = false;
        Coroutine flowRoutine;
        bool waitingManual = false;

        /// <summary>
        /// Global lifetime flag to detect the very first time this controller appears in the app run.
        /// Remains in memory across scene loads.
        /// </summary>
        static bool s_HasBootedOnce = false;

        /// <summary>
        /// Initialize all screens and, if this is not the first time we see this controller in the app,
        /// snap Animators to last frame and immediately play the first screen.
        /// </summary>
        void Start()
        {
            if (screens == null || screens.Count == 0)
            {
                Debug.LogWarning("[UI_ScreenSequenceController] No screens configured.");
                return;
            }

            // 1) Prepare start positions for all groups (extension methods on AnimationProperty list)
            for (int i = 0; i < screens.Count; i++)
                screens[i].Animations.ConfigureStartPosition();

            // 2) Force all groups to their hidden start state (prevents flashes)
            for (int i = 0; i < screens.Count; i++)
                screens[i].Animations.ForceStartPosition();

            // 3) Define start index and notify listeners
            int clampedStart = Mathf.Clamp(startIndex, 0, screens.Count - 1);
            currentIndex = clampedStart;
            onScreenChanged?.Invoke(currentIndex);

            // 4) Behavior depends on whether this is the first time or a subsequent scene load.
            if (!s_HasBootedOnce)
            {
                // First time the app sees this controller: keep your original boot behavior (no auto-reveal).
                s_HasBootedOnce = true;
                // If you ever want to auto-reveal on the very first time too, just call PlayFirstScreen() here.
                return;
            }

            // Subsequent scene loads that contain this controller:
            // Snap all child Animators to the end of their current state to avoid flickers,
            // then reveal the first screen of the sequence.
            SnapChildAnimatorsToEnd();
            PlayFirstScreen();
            
        }

        void OnEnable() => BindInputs(true);
        void OnDisable()
        {
            BindInputs(false);
            if (flowRoutine != null) { StopCoroutine(flowRoutine); flowRoutine = null; }
        }

        void EnsureFlowRoutineRunning()
        {
            if (flowRoutine != null)
                return;

            flowRoutine = StartCoroutine(Cor_ScreenFlow());
        }

        void BindInputs(bool enable)
        {
            if (nextAction != null && nextAction.action != null)
            {
                if (enable) { nextAction.action.performed += OnNextPerformed; nextAction.action.Enable(); }
                else { nextAction.action.performed -= OnNextPerformed; nextAction.action.Disable(); }
            }

            if (prevAction != null && prevAction.action != null)
            {
                if (enable) { prevAction.action.performed += OnPrevPerformed; prevAction.action.Enable(); }
                else { prevAction.action.performed -= OnPrevPerformed; prevAction.action.Disable(); }
            }
        }

        void OnNextPerformed(InputAction.CallbackContext ctx) => NextScreen();
        void OnPrevPerformed(InputAction.CallbackContext ctx) => PrevScreen();

        /// <summary>
        /// Plays the initial reveal animation for the first (startIndex) screen.
        /// Use this instead of StartTransition to ensure the very first screen animates in.
        /// </summary>
        /// <summary>
        /// Plays the initial reveal animation for the first (startIndex) screen.
        /// Use this instead of StartTransition to ensure the very first screen animates in.
        /// </summary>
        public void PlayFirstScreen()
        {
            if (screens == null || screens.Count == 0)
            {
                Debug.LogWarning("[UI_ScreenSequenceController] No screens configured.");
                return;
            }

            int clampedStart = Mathf.Clamp(startIndex, 0, screens.Count - 1);
            currentIndex = clampedStart;

            var first = screens[currentIndex].Animations;
            isTransitioning = true;

            // Reveal the initial screen and only then invoke OnShown
            first.RevealThis(() =>
            {
                isTransitioning = false;
                onScreenChanged?.Invoke(currentIndex);

                try { screens[currentIndex].OnShown?.Invoke(); }
                catch (Exception ex) { Debug.LogWarning("[UI_ScreenSequenceController] OnShown invoke failed: " + ex.Message); }

                EnsureFlowRoutineRunning();
            });
        }


        /// <summary>
        /// Main flow: time-driven. If Timed, waits the interval and advances regardless of running animations.
        /// If Manual, waits for Continue() or user navigation. It does NOT wait for AnimationProperty tweens to finish.
        /// </summary>
        IEnumerator Cor_ScreenFlow()
        {
            while (true)
            {
                if (currentIndex < 0 || currentIndex >= screens.Count)
                {
                    yield return null;
                    continue;
                }

                int snapshotIndex = currentIndex;

                var mode = screens[currentIndex].AdvanceMode;
                if (mode == ScreenAdvanceMode.Timed)
                {
                    float wait = Mathf.Max(0f, screens[currentIndex].Interval);
                    float t = 0f;
                    while (t < wait)
                    {
                        if (snapshotIndex != currentIndex)
                            break;

                        t += Time.deltaTime;
                        yield return null;
                    }

                    if (snapshotIndex == currentIndex)
                        NextScreen();
                }
                else // Manual
                {
                    waitingManual = true;
                    onWaitingManual?.Invoke(currentIndex);

                    while (waitingManual)
                    {
                        if (currentIndex != snapshotIndex)
                        {
                            waitingManual = false;
                            break;
                        }
                        yield return null;
                    }
                }

                yield return null;
            }
        }

        /// <summary>
        /// Advance to the next screen (respects loop).
        /// </summary>
        public void NextScreen()
        {
            if (!CanTransition()) return;

            int next = currentIndex + 1;
            bool cycled = false;
            if (next >= screens.Count)
            {
                if (!loop) return;
                next = 0;
                cycled = true;
            }

            StartTransition(next, cycled);
        }

        /// <summary>
        /// Return to the previous screen (respects loop).
        /// </summary>
        public void PrevScreen()
        {
            if (!CanTransition()) return;

            int prev = currentIndex - 1;
            bool cycled = false;
            if (prev < 0)
            {
                if (!loop) return;
                prev = screens.Count - 1;
                cycled = true;
            }

            StartTransition(prev, cycled);
        }

        /// <summary>
        /// Jump to a specific screen index.
        /// </summary>
        public void GoToScreen(int index)
        {
            if (index < 0 || index >= screens.Count)
                return;

            StartTransition(index, false);
        }

        /// <summary>
        /// Manual screens call this to proceed (use it in a UI Button OnClick).
        /// </summary>
        public void Continue()
        {
            if (waitingManual)
            {
                waitingManual = false;
                NextScreen();
            }
        }

        /// <summary>
        /// Returns whether we can trigger a transition command now.
        /// This no longer blocks on running animations; it only ensures bounds and basic guard.
        /// </summary>
        bool CanTransition()
        {
            if (screens == null || screens.Count == 0) return false;
            if (currentIndex < 0 || currentIndex >= screens.Count) return false;
            if (isTransitioning) return false;
            return true;
        }

        /// <summary>
        /// Starts a transition to nextIndex without waiting for current tweens to finish.
        /// Fires Hide on current and Reveal on next concurrently (fire-and-forget),
        /// updates the index immediately, and raises events right away.
        /// </summary>
        void StartTransition(int nextIndex, bool invokeCycleCompleted)
        {
            if (nextIndex < 0 || nextIndex >= screens.Count)
                return;
            if (nextIndex == currentIndex)
                return;

            waitingManual = false;
            isTransitioning = true;

            List<AnimationProperty> current = screens[currentIndex].Animations;
            List<AnimationProperty> next = screens[nextIndex].Animations;

            // Keep references to groups to invoke per-screen events
            var currGroup = screens[currentIndex];
            var nextGroup = screens[nextIndex];

            // Hide current and invoke OnHidden after the hide finishes
            current.HideThis(() =>
            {
                try { currGroup.OnHidden?.Invoke(); }
                catch (Exception ex) { Debug.LogWarning("[UI_ScreenSequenceController] OnHidden invoke failed: " + ex.Message); }
            });

            // Update index before revealing the next screen
            currentIndex = nextIndex;
            onScreenChanged?.Invoke(currentIndex);

            // Reveal next and invoke OnShown only after the reveal finishes
            next.RevealThis(() =>
            {
                try { nextGroup.OnShown?.Invoke(); }
                catch (Exception ex) { Debug.LogWarning("[UI_ScreenSequenceController] OnShown invoke failed: " + ex.Message); }
                isTransitioning = false;
            });

            if (invokeCycleCompleted)
                onCycleCompleted?.Invoke();

            EnsureFlowRoutineRunning();
        }


        /// <summary>
        /// Snap all child Animators to the end of their current state (all layers),
        /// applying the pose immediately to avoid flashes on scene load.
        /// </summary>
        void SnapChildAnimatorsToEnd()
        {
            var animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                var a = animators[i];
                if (a == null) continue;
                if (!a.isActiveAndEnabled) continue; // skip disabled Animator components

                int layerCount = Mathf.Max(1, a.layerCount);
                for (int layer = 0; layer < layerCount; layer++)
                {
                    // Get current state's full path hash and play it at normalized time 1 (last frame)
                    var st = a.GetCurrentAnimatorStateInfo(layer);
                    int fullPathHash = st.fullPathHash;
                    a.Play(fullPathHash, layer, 1f);
                }

                // Apply immediately
                a.Update(0f);
            }

            onScreenChanged?.Invoke(currentIndex);
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.2f);

            Gizmos.color = Color.Lerp(Color.white, Color.cyan, 0.5f);
            float step = 0.05f;
            int display = Mathf.Clamp(currentIndex, 0, 9);
            for (int i = 0; i <= display; i++)
            {
                Vector3 p = transform.position + Vector3.up * (0.25f + i * step);
                Gizmos.DrawCube(p, Vector3.one * 0.03f);
            }
        }
    }
}
