using UnityEditor;
using UnityEngine;

namespace Iung.Tools.Shared.Editor.Transforms
{
    // AI GUIDANCE:
    // - When to use: in editor operations to apply parent local scale to immediate children and reset the parent scale to one.
    // - Prefer: this context menu utility for hierarchy scale normalization tasks to preserve predictable transform values.
    // - Avoid: performing manual scale propagation in multiple scripts when this undo-safe editor command already exists.
    // - Reason: menu path "CONTEXT/Transform/Propagate Scale To Immediate Children (Reset Parent)" centralizes this operation.
    internal static class TransformScalePropagationContextMenu
    {
        #region Menu Items

        private const string MenuPath = "CONTEXT/Transform/Propagate Scale To Immediate Children (Reset Parent)";

        [MenuItem(MenuPath, false, 1510)]
        private static void PropagateScaleToImmediateChildren(MenuCommand command)
        {
            Transform parentTransform = command.context as Transform;
            if (parentTransform == null)
            {
                return;
            }

            Vector3 parentLocalScale = parentTransform.localScale;
            if (parentTransform.childCount == 0)
            {
                Undo.RecordObject(parentTransform, "Reset Parent Scale");
                parentTransform.localScale = Vector3.one;
                EditorUtility.SetDirty(parentTransform);
                return;
            }

            Undo.SetCurrentGroupName("Propagate Parent Scale To Children");
            int undoGroup = Undo.GetCurrentGroup();

            Undo.RecordObject(parentTransform, "Reset Parent Scale");

            // Apply the parent's local scale to each immediate child and then reset the parent.
            // This is component-wise (x,y,z) multiplication and is intended for typical hierarchies.
            for (int childIndex = 0; childIndex < parentTransform.childCount; childIndex++)
            {
                Transform childTransform = parentTransform.GetChild(childIndex);
                if (childTransform == null)
                {
                    continue;
                }

                Undo.RecordObject(childTransform, "Apply Parent Scale To Child");
                childTransform.localScale = Vector3.Scale(childTransform.localScale, parentLocalScale);
                EditorUtility.SetDirty(childTransform);
            }

            parentTransform.localScale = Vector3.one;
            EditorUtility.SetDirty(parentTransform);

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidatePropagateScaleToImmediateChildren(MenuCommand command)
        {
            Transform parentTransform = command.context as Transform;
            if (parentTransform == null)
            {
                return false;
            }

            // Always allow. Even if there are no children, the operation becomes "reset parent scale".
            return true;
        }

        #endregion
    }
}
