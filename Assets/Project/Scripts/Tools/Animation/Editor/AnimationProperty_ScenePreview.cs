using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Iung.Animation
{
    [InitializeOnLoad]
    static class AnimationProperty_ScenePreview
    {
        private static readonly List<MonoBehaviour> SelectedComponents = new List<MonoBehaviour>(64);
        private static readonly List<MonoBehaviour> ComponentBuffer = new List<MonoBehaviour>(16);
        private static bool isSubscribedToSceneGui;

        static AnimationProperty_ScenePreview()
        {
            Selection.selectionChanged -= RefreshSubscription;
            Selection.selectionChanged += RefreshSubscription;
            RefreshSubscription();
        }

        internal static void MarkDirty()
        {
            // Kept as an explicit hook for callers that change AnimationProperty data.
            // This implementation does not keep a scene-wide cache anymore, so no dirty flag is required.
        }

        internal static void MarkDirtyAndRepaint()
        {
            RefreshSubscription();
            SceneView.RepaintAll();
        }

        static void RefreshSubscription()
        {
            bool shouldSubscribe = HasPreviewEnabledInCurrentSelection();
            if (shouldSubscribe == isSubscribedToSceneGui)
                return;

            SceneView.duringSceneGui -= OnSceneGUI;
            if (shouldSubscribe)
                SceneView.duringSceneGui += OnSceneGUI;

            isSubscribedToSceneGui = shouldSubscribe;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            Event currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type != EventType.Repaint)
                return;

            Profiler.BeginSample("Iung.Animation.ScenePreview.OnSceneGUI");
            try
            {
                CollectSelectedSceneComponents(SelectedComponents);
                if (SelectedComponents.Count == 0)
                    return;

                for (int i = 0; i < SelectedComponents.Count; i++)
                {
                    MonoBehaviour component = SelectedComponents[i];
                    if (!IsSceneComponent(component))
                        continue;

                    DrawPreviewForComponent(component);
                }
            }
            finally
            {
                SelectedComponents.Clear();
                Profiler.EndSample();
            }
        }

        static bool HasPreviewEnabledInCurrentSelection()
        {
            CollectSelectedSceneComponents(SelectedComponents);
            try
            {
                for (int i = 0; i < SelectedComponents.Count; i++)
                {
                    MonoBehaviour component = SelectedComponents[i];
                    if (!IsSceneComponent(component) || !ComponentHasEnabledPreview(component))
                        continue;

                    return true;
                }

                return false;
            }
            finally
            {
                SelectedComponents.Clear();
            }
        }

        static void CollectSelectedSceneComponents(List<MonoBehaviour> components)
        {
            components.Clear();

            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
                return;

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                GameObject selectedObject = selectedObjects[i];
                if (!IsSceneGameObject(selectedObject))
                    continue;

                ComponentBuffer.Clear();
                selectedObject.GetComponents(ComponentBuffer);

                for (int componentIndex = 0; componentIndex < ComponentBuffer.Count; componentIndex++)
                {
                    MonoBehaviour component = ComponentBuffer[componentIndex];
                    if (component != null)
                    {
                        components.Add(component);
                    }
                }
            }

            ComponentBuffer.Clear();
        }

        static bool ComponentHasEnabledPreview(MonoBehaviour component)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (!LooksLikeAnimationProperty(iterator))
                    continue;

                SerializedProperty propertyCopy = iterator.Copy();
                if (!AnimationProperty_PropertyDrawer.SupportsHidePreview(propertyCopy))
                    continue;

                string previewKey = AnimationProperty_PropertyDrawer.GetPreviewStateKey(propertyCopy);
                if (SessionState.GetBool(previewKey, false))
                    return true;
            }

            return false;
        }

        static void DrawPreviewForComponent(MonoBehaviour component)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (!LooksLikeAnimationProperty(iterator))
                    continue;

                SerializedProperty propertyCopy = iterator.Copy();
                if (!AnimationProperty_PropertyDrawer.SupportsHidePreview(propertyCopy))
                    continue;

                string previewKey = AnimationProperty_PropertyDrawer.GetPreviewStateKey(propertyCopy);
                if (!SessionState.GetBool(previewKey, false))
                    continue;

                AnimationProperty_PropertyDrawer.DrawHidePreviewInScene(propertyCopy);
            }
        }

        static bool LooksLikeAnimationProperty(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.Generic)
                return false;

            return property.FindPropertyRelative("_object") != null &&
                   property.FindPropertyRelative("_animationType") != null &&
                   property.FindPropertyRelative("time") != null;
        }

        static bool IsSceneComponent(MonoBehaviour component)
        {
            if (component == null)
                return false;

            if (EditorUtility.IsPersistent(component))
                return false;

            if (component.gameObject == null || !component.gameObject.scene.IsValid() || !component.gameObject.scene.isLoaded)
                return false;

            if ((component.hideFlags & HideFlags.HideAndDontSave) != 0)
                return false;

            return true;
        }

        static bool IsSceneGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            if (EditorUtility.IsPersistent(gameObject))
                return false;

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
                return false;

            if ((gameObject.hideFlags & HideFlags.HideAndDontSave) != 0)
                return false;

            return true;
        }
    }
}
