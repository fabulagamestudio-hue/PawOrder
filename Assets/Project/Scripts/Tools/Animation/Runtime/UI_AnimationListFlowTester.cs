using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Iung.Animation
{
    /// <summary>
    /// Simple scene helper to test Show/Hide flows for a list of AnimationProperty entries.
    /// Hook ShowList/HideList on UI Buttons to validate animation sequencing.
    /// </summary>
    public class UI_AnimationListFlowTester : MonoBehaviour
    {
        [Header("Target List")]
        [SerializeField] private List<AnimationProperty> animations = new List<AnimationProperty>();

        [Header("Startup")]
        [SerializeField] private bool configureStartOnAwake = true;
        [SerializeField] private bool forceHiddenOnAwake = true;

        [Header("Flow Guard")]
        [SerializeField] private bool ignoreRequestsWhileAnimating = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onShowStarted;
        [SerializeField] private UnityEvent onShowCompleted;
        [SerializeField] private UnityEvent onHideCompleted;

        private void Awake()
        {
            if (!HasValidList())
            {
                return;
            }

            if (configureStartOnAwake)
            {
                animations.ConfigureStartPosition();
            }

            if (forceHiddenOnAwake)
            {
                animations.ForceStartPosition();
            }
        }

        public void ShowList()
        {
            if (!CanRunRequest())
            {
                return;
            }

            animations.RevealThis(
                OnFinish: () => onShowCompleted?.Invoke(),
                OnStart: () => onShowStarted?.Invoke());
        }

        public void HideList()
        {
            if (!CanRunRequest())
            {
                return;
            }

            animations.HideThis(OnFinish: () => onHideCompleted?.Invoke());
        }

        public void ForceHidden()
        {
            if (!HasValidList())
            {
                return;
            }

            animations.ForceStartPosition();
        }

        public void ForceShown()
        {
            if (!HasValidList())
            {
                return;
            }

            animations.ForceEndPosition();
        }

        private bool CanRunRequest()
        {
            if (!HasValidList())
            {
                return false;
            }

            if (ignoreRequestsWhileAnimating && animations.IsAnimating())
            {
                return false;
            }

            return true;
        }

        private bool HasValidList()
        {
            if (animations == null || animations.Count == 0)
            {
                Debug.LogWarning("[UI_AnimationListFlowTester] Animation list is empty.", this);
                return false;
            }

            return true;
        }
    }
}
