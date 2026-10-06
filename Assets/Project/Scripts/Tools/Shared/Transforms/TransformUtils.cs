using System.Collections.Generic;
using UnityEngine;

namespace Iung.Tools.Shared.Transforms
{
    // AI GUIDANCE:
    // - When to use: when creating standard hierarchy folders, clearing children, querying typed children, or finding nearest transforms.
    // - Prefer: using TransformUtils for hierarchy manipulation patterns so object organization stays consistent and easier to maintain.
    // - Avoid: creating custom child traversal helpers for common operations that are already covered here.
    // - Reason: GetOrCreateFolderParent relies on the stable root name "Scenary Objects" to avoid duplicated hierarchy roots.
    public static class TransformUtils
    {
        /// <summary>
        /// Returns the folder transform inside the "Scenary Objects" root, creating it if necessary.
        /// </summary>
        public static Transform GetOrCreateFolderParent(string folderName)
        {
            const string rootName = "Scenary Objects";

            GameObject root = GameObject.Find(rootName);
            if (root == null)
                root = new GameObject(rootName);

            Transform folder = root.transform.Find(folderName);
            if (folder == null)
            {
                GameObject folderGO = new GameObject(folderName);
                folderGO.transform.SetParent(root.transform);
                folder = folderGO.transform;
            }

            return folder;
        }

        public static void DestroyAllChildren(this Transform parent, bool includeParent = false, bool includeInactive = false)
        {
            var buffer = new List<Transform>(parent.GetComponentsInChildren<Transform>(includeInactive));
            foreach (var child in buffer)
            {
                if (!includeParent && child == parent) continue;
                Object.Destroy(child.gameObject);
            }
        }

        public static List<T> GetChildrenOfType<T>(this Transform parent, bool includeParent = false)
        {
            var result = new List<T>();
            foreach (var child in parent.GetComponentsInChildren<Transform>())
            {
                if (!includeParent && child == parent) continue;
                if (child.TryGetComponent<T>(out var component))
                    result.Add(component);
            }
            return result;
        }

        public static Transform GetNearestTo(this List<Transform> list, Transform reference)
        {
            if (list == null || list.Count == 0) return null;
            var nearest = list[0];
            float minDistanceSqr = (nearest.position - reference.position).sqrMagnitude;

            for (int i = 1; i < list.Count; i++)
            {
                float distSqr = (list[i].position - reference.position).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    nearest = list[i];
                }
            }

            return nearest;
        }
    }
}
