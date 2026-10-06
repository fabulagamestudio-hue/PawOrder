using System;
using System.Collections.Generic;
using UnityEngine;

namespace Iung.Tools.Shared.Pooling
{
    // AI GUIDANCE:
    // - When to use: when pooled prefabs must restore transform, activation, and behaviour enabled states to their initial snapshot.
    // - Prefer: adding PoolStateRestorer to complex pooled objects instead of writing custom reset logic per spawn or despawn workflow.
    // - Avoid: manually resetting each child transform or behaviour in calling code when this component can restore the captured snapshot.
    // - Reason: snapshot capture is lazy and runs once, with includeRoot flags controlling integration with external pool activation ownership.
    public class PoolStateRestorer : MonoBehaviour
    {
        #region Types

        [Serializable]
        private struct TransformSnapshot
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
            public bool ActiveSelf;
        }

        [Serializable]
        private struct BehaviourSnapshot
        {
            public Behaviour Behaviour;
            public bool Enabled;
        }

        #endregion

        #region Fields

        [Header("Options")]
        [SerializeField, Tooltip("If true, the root Transform local position/rotation/scale will be restored as well.")]
        private bool includeRootTransform = true;

        [SerializeField, Tooltip("If true, the root GameObject active state will be restored as well. Keep this false when an external pool controls activation.")]
        private bool includeRootActiveState = false;

        [SerializeField, Tooltip("If true, restores Behaviour.enabled for all captured components (Colliders, Renderers, MonoBehaviours, etc.).")]
        private bool restoreBehaviourEnabledState = true;

        [SerializeField, Tooltip("If true, calls ResetForReuse() on all components in children that implement IPoolResettable.")]
        private bool callResettableComponents = true;

        private readonly List<TransformSnapshot> transformSnapshots = new List<TransformSnapshot>(64);
        private readonly List<BehaviourSnapshot> behaviourSnapshots = new List<BehaviourSnapshot>(64);
        private readonly List<IPoolResettable> resettables = new List<IPoolResettable>(16);

        private bool hasSnapshot;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            CaptureSnapshotIfNeeded();
        }

        #endregion

        #region Public API

        public void CaptureSnapshotIfNeeded()
        {
            if (hasSnapshot) return;

            transformSnapshots.Clear();
            behaviourSnapshots.Clear();
            resettables.Clear();

            Transform root = transform;
            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform current = allTransforms[i];
                if (current == null) continue;

                TransformSnapshot snapshot = new TransformSnapshot
                {
                    Transform = current,
                    LocalPosition = current.localPosition,
                    LocalRotation = current.localRotation,
                    LocalScale = current.localScale,
                    ActiveSelf = current.gameObject.activeSelf
                };

                transformSnapshots.Add(snapshot);
            }

            if (restoreBehaviourEnabledState)
            {
                Behaviour[] allBehaviours = GetComponentsInChildren<Behaviour>(true);
                for (int i = 0; i < allBehaviours.Length; i++)
                {
                    Behaviour behaviour = allBehaviours[i];
                    if (behaviour == null) continue;
                    if (behaviour == this) continue;

                    BehaviourSnapshot snapshot = new BehaviourSnapshot
                    {
                        Behaviour = behaviour,
                        Enabled = behaviour.enabled
                    };

                    behaviourSnapshots.Add(snapshot);
                }
            }

            if (callResettableComponents)
            {
                MonoBehaviour[] allMonoBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < allMonoBehaviours.Length; i++)
                {
                    if (allMonoBehaviours[i] is not IPoolResettable resettable)
                        continue;

                    resettables.Add(resettable);
                }
            }

            hasSnapshot = true;
        }

        public void RestoreToInitialState()
        {
            if (!hasSnapshot)
                CaptureSnapshotIfNeeded();

            Transform root = transform;

            for (int i = 0; i < transformSnapshots.Count; i++)
            {
                TransformSnapshot snapshot = transformSnapshots[i];
                if (snapshot.Transform == null) continue;

                bool isRoot = snapshot.Transform == root;

                if (!isRoot || includeRootTransform)
                {
                    snapshot.Transform.localPosition = snapshot.LocalPosition;
                    snapshot.Transform.localRotation = snapshot.LocalRotation;
                    snapshot.Transform.localScale = snapshot.LocalScale;
                }

                if (!isRoot || includeRootActiveState)
                    snapshot.Transform.gameObject.SetActive(snapshot.ActiveSelf);
            }

            if (restoreBehaviourEnabledState)
            {
                for (int i = 0; i < behaviourSnapshots.Count; i++)
                {
                    BehaviourSnapshot snapshot = behaviourSnapshots[i];
                    if (snapshot.Behaviour == null) continue;
                    snapshot.Behaviour.enabled = snapshot.Enabled;
                }
            }

            if (callResettableComponents)
            {
                for (int i = 0; i < resettables.Count; i++)
                {
                    if (resettables[i] == null) continue;
                    resettables[i].ResetForReuse();
                }
            }
        }

        #endregion
    }
}
