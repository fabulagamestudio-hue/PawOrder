using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Iung.Tools.Shared.Editor.Transforms
{
    // AI GUIDANCE:
    // - When to use: in editor workflows to recenter a parent Transform to the average world position of its immediate children.
    // - Prefer: this context action when reorganizing hierarchy pivots without visually moving child objects in the scene.
    // - Avoid: using it on prefab assets or parents without children, because the operation is intended for loaded scene instances.
    // - Reason: menu path "CONTEXT/Transform/Center Parent To Children Average" centralizes this pivot-adjustment workflow with Undo support.
    internal static class TransformCenterParentContextMenu
    {
        #region Menu Items

        private const string MenuPath = "CONTEXT/Transform/Center Parent To Children Average";
        private const string UndoOperationName = "Center Parent To Children Average";

        [MenuItem(MenuPath, false, 1500)]
        private static void CenterParentToChildrenAverage(MenuCommand command)
        {
            Transform parentTransform = command.context as Transform;
            if (!TryGetValidParentTransform(parentTransform))
            {
                return;
            }

            int childCount = parentTransform.childCount;
            Vector3 averageWorldPosition = CalculateAverageChildWorldPosition(parentTransform, childCount);
            List<Vector3> childWorldPositions = CacheChildWorldPositions(parentTransform, childCount);

            Undo.SetCurrentGroupName(UndoOperationName);
            int undoGroup = Undo.GetCurrentGroup();

            Undo.RecordObject(parentTransform, UndoOperationName);
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                Undo.RecordObject(parentTransform.GetChild(childIndex), UndoOperationName);
            }

            parentTransform.position = averageWorldPosition;
            EditorUtility.SetDirty(parentTransform);

            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                Transform childTransform = parentTransform.GetChild(childIndex);
                childTransform.position = childWorldPositions[childIndex];
                EditorUtility.SetDirty(childTransform);
            }

            EditorSceneManager.MarkSceneDirty(parentTransform.gameObject.scene);
            Selection.activeTransform = parentTransform;
            EditorGUIUtility.PingObject(parentTransform);
            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateCenterParentToChildrenAverage(MenuCommand command)
        {
            Transform parentTransform = command.context as Transform;
            return TryGetValidParentTransform(parentTransform);
        }

        #endregion

        #region Internal Logic

        private static bool TryGetValidParentTransform(Transform parentTransform)
        {
            if (parentTransform == null)
            {
                return false;
            }

            if (parentTransform.childCount == 0)
            {
                return false;
            }

            GameObject parentGameObject = parentTransform.gameObject;
            if (parentGameObject == null)
            {
                return false;
            }

            if (EditorUtility.IsPersistent(parentGameObject))
            {
                return false;
            }

            return parentGameObject.scene.IsValid() && parentGameObject.scene.isLoaded;
        }

        private static Vector3 CalculateAverageChildWorldPosition(Transform parentTransform, int childCount)
        {
            Vector3 accumulatedPosition = Vector3.zero;
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                accumulatedPosition += parentTransform.GetChild(childIndex).position;
            }

            return accumulatedPosition / childCount;
        }

        private static List<Vector3> CacheChildWorldPositions(Transform parentTransform, int childCount)
        {
            List<Vector3> childWorldPositions = new List<Vector3>(childCount);
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                childWorldPositions.Add(parentTransform.GetChild(childIndex).position);
            }

            return childWorldPositions;
        }

        #endregion
    }
}
