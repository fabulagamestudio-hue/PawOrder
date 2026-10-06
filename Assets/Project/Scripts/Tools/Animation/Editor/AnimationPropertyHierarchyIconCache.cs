using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Iung.Animation
{
    [InitializeOnLoad]
    internal static class AnimationPropertyHierarchyIconCache
    {
        private const string IconAssetPath = "Assets/Project/Tools/Animation/Editor/UI/Icon.png";
        private const float IconSize = 14f;
        private const float PreviewIconSize = 14f;
        private const float ConflictBadgeSize = 10f;
        private const float ConflictLineThickness = 1.5f;
        private const double RebuildDebounceSeconds = 0.15d;

        private static readonly Dictionary<int, AnimatedObjectInfo> AnimatedObjects = new Dictionary<int, AnimatedObjectInfo>();
        private static Texture iconTexture;
        private static Texture previewIconTexture;
        private static bool isDirty = true;
        private static bool hierarchyRepaintQueued;
        private static double nextAllowedRebuildTime;

        static AnimationPropertyHierarchyIconCache()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
            EditorApplication.hierarchyChanged += MarkDirtyAndRepaint;
            Undo.undoRedoPerformed += MarkDirtyAndRepaint;
            AssemblyReloadEvents.afterAssemblyReload += MarkDirtyAndRepaint;
        }

        private sealed class AnimatedObjectInfo
        {
            public readonly HashSet<string> ControllerNames = new HashSet<string>(StringComparer.Ordinal);
            public int ReferenceCount;
            public int PreviewReferenceCount;
            public bool IsDirectTarget;
        }

        public static void MarkDirtyAndRepaint()
        {
            isDirty = true;
            nextAllowedRebuildTime = EditorApplication.timeSinceStartup + RebuildDebounceSeconds;
            QueueHierarchyRepaint();
        }

        private static void QueueHierarchyRepaint()
        {
            if (hierarchyRepaintQueued)
            {
                return;
            }

            hierarchyRepaintQueued = true;
            EditorApplication.delayCall -= RepaintHierarchyWindowOnce;
            EditorApplication.delayCall += RepaintHierarchyWindowOnce;
        }

        private static void RepaintHierarchyWindowOnce()
        {
            hierarchyRepaintQueued = false;
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceId, Rect selectionRect)
        {
            EnsureCache();

            if (!AnimatedObjects.TryGetValue(instanceId, out AnimatedObjectInfo animatedObjectInfo))
            {
                return;
            }

            float right = selectionRect.xMax - 2f;

            if (animatedObjectInfo.IsDirectTarget && animatedObjectInfo.ReferenceCount > 0)
            {
                Rect iconRect = new Rect(
                    right - IconSize,
                    selectionRect.y + ((selectionRect.height - IconSize) * 0.5f),
                    IconSize,
                    IconSize);

                Rect previewRect = new Rect(
                    iconRect.x - PreviewIconSize - 2f,
                    selectionRect.y + ((selectionRect.height - PreviewIconSize) * 0.5f),
                    PreviewIconSize,
                    PreviewIconSize);

                GUI.Label(iconRect, new GUIContent(GetIconTexture(), BuildTooltip(animatedObjectInfo)));

                if (HasPreview(animatedObjectInfo))
                {
                    GUI.Label(previewRect, new GUIContent(GetPreviewIconTexture(), BuildPreviewTooltip(animatedObjectInfo)));
                }

                if (HasConflict(animatedObjectInfo))
                {
                    DrawConflictOverlay(iconRect);
                }
            }
            else if (HasPreview(animatedObjectInfo))
            {
                Rect previewRect = new Rect(
                    right - PreviewIconSize,
                    selectionRect.y + ((selectionRect.height - PreviewIconSize) * 0.5f),
                    PreviewIconSize,
                    PreviewIconSize);

                GUI.Label(previewRect, new GUIContent(GetPreviewIconTexture(), BuildPreviewTooltip(animatedObjectInfo)));
            }
        }

        private static void EnsureCache()
        {
            if (!isDirty)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < nextAllowedRebuildTime)
            {
                QueueHierarchyRepaint();
                return;
            }

            RebuildCache();
        }

        private static void RebuildCache()
        {
            AnimatedObjects.Clear();

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                isDirty = false;
                return;
            }

            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                CollectAnimatedObjectsFromHierarchy(rootObjects[rootIndex]);
            }

            isDirty = false;
        }

        private static void CollectAnimatedObjectsFromHierarchy(GameObject rootObject)
        {
            if (rootObject == null)
            {
                return;
            }

            UI_ScreenSequenceController[] screenSequenceControllers = rootObject.GetComponentsInChildren<UI_ScreenSequenceController>(true);
            for (int controllerIndex = 0; controllerIndex < screenSequenceControllers.Length; controllerIndex++)
            {
                UI_ScreenSequenceController controller = screenSequenceControllers[controllerIndex];
                if (controller != null)
                {
                    CollectAnimatedObjectsFromSerializedObject(new SerializedObject(controller), controller.gameObject.name);
                }
            }

            UI_AnimationListFlowTester[] animationListFlowTesters = rootObject.GetComponentsInChildren<UI_AnimationListFlowTester>(true);
            for (int testerIndex = 0; testerIndex < animationListFlowTesters.Length; testerIndex++)
            {
                UI_AnimationListFlowTester tester = animationListFlowTesters[testerIndex];
                if (tester != null)
                {
                    CollectAnimatedObjectsFromSerializedObject(new SerializedObject(tester), tester.gameObject.name);
                }
            }
        }

        private static void CollectAnimatedObjectsFromSerializedObject(SerializedObject serializedObject, string controllerName)
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                SerializedProperty objectProperty = iterator.FindPropertyRelative("_object");
                SerializedProperty animationTypeProperty = iterator.FindPropertyRelative("_animationType");
                if (objectProperty == null || animationTypeProperty == null)
                {
                    continue;
                }

                GameObject animatedObject = objectProperty.objectReferenceValue as GameObject;
                if (animatedObject == null)
                {
                    continue;
                }

                bool previewActive = false;
                string previewStateKey = AnimationProperty_PropertyDrawer.GetPreviewStateKey(iterator);
                if (!string.IsNullOrEmpty(previewStateKey))
                {
                    previewActive = SessionState.GetBool(previewStateKey, false);
                }

                AddReferenceToHierarchy(animatedObject, controllerName, previewActive);
            }
        }

        private static void AddReferenceToHierarchy(GameObject animatedObject, string controllerName, bool previewActive)
        {
            if (animatedObject == null)
            {
                return;
            }

            Transform current = animatedObject.transform;
            bool isDirect = true;
            while (current != null)
            {
                AddReference(current.gameObject.GetInstanceID(), controllerName, previewActive, isDirect);
                current = current.parent;
                isDirect = false;
            }
        }

        private static void AddReference(int animatedObjectId, string controllerName, bool previewActive, bool isDirectTarget)
        {
            if (!AnimatedObjects.TryGetValue(animatedObjectId, out AnimatedObjectInfo animatedObjectInfo))
            {
                animatedObjectInfo = new AnimatedObjectInfo();
                AnimatedObjects.Add(animatedObjectId, animatedObjectInfo);
            }

            animatedObjectInfo.ReferenceCount++;
            if (previewActive)
            {
                animatedObjectInfo.PreviewReferenceCount++;
            }

            if (isDirectTarget)
            {
                animatedObjectInfo.IsDirectTarget = true;
            }

            if (!string.IsNullOrWhiteSpace(controllerName))
            {
                animatedObjectInfo.ControllerNames.Add(controllerName);
            }
        }

        private static bool HasConflict(AnimatedObjectInfo animatedObjectInfo)
        {
            return animatedObjectInfo != null && animatedObjectInfo.ReferenceCount > 1;
        }

        private static bool HasPreview(AnimatedObjectInfo animatedObjectInfo)
        {
            return animatedObjectInfo != null && animatedObjectInfo.PreviewReferenceCount > 0;
        }

        private static string BuildTooltip(AnimatedObjectInfo animatedObjectInfo)
        {
            if (animatedObjectInfo == null)
            {
                return "Controlled by Unknown";
            }

            string joinedNames = animatedObjectInfo.ControllerNames.Count > 0
                ? string.Join(", ", animatedObjectInfo.ControllerNames.OrderBy(name => name, StringComparer.Ordinal))
                : "Unknown";

            if (HasConflict(animatedObjectInfo))
            {
                return $"Conflict: {animatedObjectInfo.ReferenceCount} entries controlled by {joinedNames}";
            }

            return $"Controlled by {joinedNames}";
        }

        private static string BuildPreviewTooltip(AnimatedObjectInfo animatedObjectInfo)
        {
            if (animatedObjectInfo == null || animatedObjectInfo.PreviewReferenceCount <= 0)
            {
                return "Preview inactive";
            }

            if (animatedObjectInfo.PreviewReferenceCount == 1)
            {
                return "Preview active";
            }

            return $"Preview active on {animatedObjectInfo.PreviewReferenceCount} entries";
        }

        private static void DrawConflictOverlay(Rect iconRect)
        {
            Rect badgeRect = new Rect(
                iconRect.xMax - ConflictBadgeSize + 1f,
                iconRect.y - 1f,
                ConflictBadgeSize,
                ConflictBadgeSize);

            Color previousColor = Handles.color;
            Vector3 badgeCenter = badgeRect.center;
            float radius = badgeRect.width * 0.5f;
            Vector3 slashStart = new Vector3(badgeRect.x + 2f, badgeRect.yMax - 2f, 0f);
            Vector3 slashEnd = new Vector3(badgeRect.xMax - 2f, badgeRect.y + 2f, 0f);

            Handles.BeginGUI();
            Handles.color = new Color(0.85f, 0.15f, 0.15f, 1f);
            Handles.DrawSolidDisc(badgeCenter, Vector3.forward, radius);
            Handles.color = Color.white;
            Handles.DrawAAPolyLine(ConflictLineThickness, slashStart, slashEnd);
            Handles.EndGUI();

            Handles.color = previousColor;
        }

        private static Texture GetIconTexture()
        {
            if (iconTexture != null)
            {
                return iconTexture;
            }

            iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
            if (iconTexture != null)
            {
                return iconTexture;
            }

            GUIContent fallbackIcon = EditorGUIUtility.IconContent("d_Animation.Record");
            iconTexture = fallbackIcon.image;
            return iconTexture;
        }

        private static Texture GetPreviewIconTexture()
        {
            if (previewIconTexture != null)
            {
                return previewIconTexture;
            }

            GUIContent fallbackIcon = EditorGUIUtility.IconContent("Camera Icon");
            previewIconTexture = fallbackIcon.image;

            if (previewIconTexture == null)
            {
                fallbackIcon = EditorGUIUtility.IconContent("d_SceneViewCamera");
                previewIconTexture = fallbackIcon.image;
            }

            return previewIconTexture;
        }
    }
}
