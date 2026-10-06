// File: RectTransformAnchorEditorUtility.cs
// Location suggestion: Assets/Project/Scripts/Tools/Editor/RectTransformAnchorEditorUtility.cs
// Scope: Editor-only

using UnityEngine;
using UnityEditor;

namespace Iung.Tools.Shared.Editor.UI
{
    // AI GUIDANCE:
    // - When to use: in UI editing workflows that convert current RectTransform bounds to anchors and clear offsets.
    // - Prefer: this utility when adjusting anchors while preserving visual placement instead of writing one-off anchor conversion scripts.
    // - Avoid: modifying anchors by hand in batch workflows where this menu tool can process selection safely with undo support.
    // - Reason: it requires a RectTransform parent with non-zero size and encapsulates the anchor conversion safely.
    public static class RectTransformAnchorEditorUtility
    {
        #region Fields
        private const string UndoOperationName = "Anchors To Corners (Zero Position)";
        #endregion

        #region Properties
        // No runtime properties required for this editor utility.
        #endregion

        #region Unity Messages
        // Not applicable in Editor utility.
        #endregion

        #region Public API
        /// <summary>
        /// Process all currently selected RectTransforms.
        /// </summary>
        [MenuItem("Iung/RectTransform/Anchors To Corners (Zero Position)")]
        public static void AnchorsToCornersForSelection()
        {
            int processed = 0;
            int skippedNoParent = 0;
            int skippedZeroParentSize = 0;
            int skippedNotRectTransform = 0;

            var transforms = Selection.transforms;
            foreach (var t in transforms)
            {
                if (!(t is RectTransform rt))
                {
                    skippedNotRectTransform++;
                    continue;
                }

                if (!(rt.parent is RectTransform parent))
                {
                    skippedNoParent++;
                    continue;
                }

                Vector2 parentSize = parent.rect.size;
                if (Mathf.Approximately(parentSize.x, 0f) || Mathf.Approximately(parentSize.y, 0f))
                {
                    skippedZeroParentSize++;
                    continue;
                }

                ApplyAnchorsToCorners(rt, parentSize);
                processed++;
            }

            if (processed > 0)
            {
                Debug.Log($"[AnchorsToCorners] Updated: {processed}. Skipped (NoParent: {skippedNoParent}, ZeroParentSize: {skippedZeroParentSize}, NotRectTransform: {skippedNotRectTransform}).");
            }
            else
            {
                Debug.LogWarning($"[AnchorsToCorners] No RectTransforms updated. Skipped (NoParent: {skippedNoParent}, ZeroParentSize: {skippedZeroParentSize}, NotRectTransform: {skippedNotRectTransform}).");
            }
        }

        /// <summary>
        /// Context menu on RectTransform component.
        /// </summary>
        [MenuItem("CONTEXT/RectTransform/Anchors To Corners (Zero Position)")]
        private static void AnchorsToCornersContext(MenuCommand command)
        {
            if (command.context is RectTransform rt)
            {
                if (!(rt.parent is RectTransform parent))
                {
                    Debug.LogWarning("[AnchorsToCorners] Skipped: selected RectTransform has no parent.");
                    return;
                }

                Vector2 parentSize = parent.rect.size;
                if (Mathf.Approximately(parentSize.x, 0f) || Mathf.Approximately(parentSize.y, 0f))
                {
                    Debug.LogWarning("[AnchorsToCorners] Skipped: parent has zero width or height.");
                    return;
                }

                ApplyAnchorsToCorners(rt, parentSize);
                Debug.Log("[AnchorsToCorners] Updated 1 RectTransform via context menu.");
            }
        }
        #endregion

        #region Internal Logic
        /// <summary>
        /// Moves anchors to match the current rect in parent space and zeros offsets/anchoredPosition.
        /// </summary>
        private static void ApplyAnchorsToCorners(RectTransform target, Vector2 parentSize)
        {
            Undo.RecordObject(target, UndoOperationName);

            // Compute new normalized anchors based on current offsets and parent size.
            Vector2 invParentSize = new Vector2(1f / parentSize.x, 1f / parentSize.y);

            Vector2 newAnchorMin = new Vector2(
                target.anchorMin.x + target.offsetMin.x * invParentSize.x,
                target.anchorMin.y + target.offsetMin.y * invParentSize.y
            );

            Vector2 newAnchorMax = new Vector2(
                target.anchorMax.x + target.offsetMax.x * invParentSize.x,
                target.anchorMax.y + target.offsetMax.y * invParentSize.y
            );

            // Apply anchors.
            target.anchorMin = newAnchorMin;
            target.anchorMax = newAnchorMax;

            // Zero offsets and anchored position to lock the rect to the new anchors.
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            target.anchoredPosition = Vector2.zero;

            EditorUtility.SetDirty(target);
        }
        #endregion

        #region DoTween
        // Not applicable; no animations in editor utility.
        #endregion

        #region Events
        // Not applicable for this tool.
        #endregion

        #region Gizmos/Debug
        // No gizmos for editor menu utility.
        #endregion

        #region Editor
        // All editor bindings are declared above via MenuItem attributes.
        #endregion
    }

    /*
     * Technical note:
     * - This tool requires the target to have a RectTransform parent with non-zero width and height.
     * - Layout components (e.g., LayoutGroup, ContentSizeFitter) may reflow after this operation and change results.
     * - The visual layout is preserved while converting to fully anchored space with zero offsets.
     */
}
