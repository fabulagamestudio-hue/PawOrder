using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Iung.Tools.Shared.Editor.Creation
{
    // AI GUIDANCE:
    // - When to use: in editor workflows that create scene GameObjects from selected MonoScript assets or add selected scripts to an existing scene object.
    // - Prefer: this editor tool path for script-driven scene object creation instead of ad-hoc temporary editor utilities.
    // - Avoid: instantiating runtime objects through custom editor code when this standardized menu already handles selection validation and undo.
    // - Reason: menu paths "Assets/Iung/Create at" and "Assets/Iung/Add to selected" provide a shared, predictable workflow.
    internal static class ProjectScriptsCreateAtTool
    {
        #region Fields

        private const string CreateAtMenuPath = "Assets/Iung/Create at";
        private const string AddToSelectedMenuPath = "Assets/Iung/Add to selected";
        private const string CreateUndoOperationName = "Create GameObject From Selected Scripts";
        private const string AddToSelectedUndoOperationName = "Add Selected Scripts To GameObject";
        private const string ParentUndoOperationName = "Parent Created Object";
        private const string ResetTransformUndoOperationName = "Reset Created Object Transform";
        private const string CreatedObjectPrefix = "@";

        #endregion

        #region Properties
        // No editor properties required.
        #endregion

        #region Unity Messages
        // Not applicable for this editor utility.
        #endregion

        #region Public API

        [MenuItem(CreateAtMenuPath, false, 2000)]
        private static void CreateAt()
        {
            List<Type> componentTypes = CollectValidComponentTypes();
            if (componentTypes.Count == 0)
            {
                Debug.LogWarning("[ProjectScriptsCreateAtTool] No valid MonoBehaviour scripts were found in the current Project selection.");
                return;
            }

            string gameObjectName = BuildCreatedObjectName(componentTypes[0]);
            Transform parentTransform = GetFirstSelectedSceneGameObjectTransform();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(CreateUndoOperationName);
            int undoGroup = Undo.GetCurrentGroup();

            GameObject createdObject = new GameObject(gameObjectName);
            Undo.RegisterCreatedObjectUndo(createdObject, CreateUndoOperationName);

            PlaceObjectInCurrentStage(createdObject);
            ApplyParent(createdObject.transform, parentTransform);
            ApplyDefaultLocalTransform(createdObject.transform);
            AddComponentsToGameObject(createdObject, componentTypes, ignoreExistingComponents: false);

            Selection.activeGameObject = createdObject;
            EditorGUIUtility.PingObject(createdObject);
            EditorSceneManager.MarkSceneDirty(createdObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(CreateAtMenuPath, true)]
        private static bool ValidateCreateAt()
        {
            return TryGetFirstValidMonoBehaviourType(out _);
        }

        [MenuItem(AddToSelectedMenuPath, false, 2001)]
        private static void AddToSelected()
        {
            List<Type> componentTypes = CollectValidComponentTypes();
            if (componentTypes.Count == 0)
            {
                Debug.LogWarning("[ProjectScriptsCreateAtTool] No valid MonoBehaviour scripts were found in the current Project selection.");
                return;
            }

            GameObject targetGameObject = GetFirstSelectedSceneGameObject();
            if (targetGameObject == null)
            {
                Debug.LogWarning("[ProjectScriptsCreateAtTool] No valid scene GameObject is selected to receive the requested scripts.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(AddToSelectedUndoOperationName);
            int undoGroup = Undo.GetCurrentGroup();

            int addedComponentCount = AddComponentsToGameObject(targetGameObject, componentTypes, ignoreExistingComponents: true);
            if (addedComponentCount == 0)
            {
                Debug.LogWarning("[ProjectScriptsCreateAtTool] The selected GameObject already contains all valid selected script types.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            Selection.activeGameObject = targetGameObject;
            EditorGUIUtility.PingObject(targetGameObject);
            EditorSceneManager.MarkSceneDirty(targetGameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(AddToSelectedMenuPath, true)]
        private static bool ValidateAddToSelected()
        {
            return TryGetFirstValidMonoBehaviourType(out _) && GetFirstSelectedSceneGameObject() != null;
        }

        #endregion

        #region Internal Logic

        private static List<Type> CollectValidComponentTypes()
        {
            List<Type> componentTypes = new List<Type>();
            MonoScript[] selectedScripts = Selection.GetFiltered<MonoScript>(SelectionMode.Assets);

            for (int index = 0; index < selectedScripts.Length; index++)
            {
                MonoScript selectedScript = selectedScripts[index];
                if (!TryGetValidMonoBehaviourType(selectedScript, out Type componentType))
                {
                    continue;
                }

                if (componentTypes.Contains(componentType))
                {
                    continue;
                }

                componentTypes.Add(componentType);
            }

            return componentTypes;
        }

        private static bool TryGetFirstValidMonoBehaviourType(out Type componentType)
        {
            MonoScript[] selectedScripts = Selection.GetFiltered<MonoScript>(SelectionMode.Assets);
            for (int index = 0; index < selectedScripts.Length; index++)
            {
                if (TryGetValidMonoBehaviourType(selectedScripts[index], out componentType))
                {
                    return true;
                }
            }

            componentType = null;
            return false;
        }

        private static bool TryGetValidMonoBehaviourType(MonoScript selectedScript, out Type componentType)
        {
            componentType = null;
            if (selectedScript == null)
            {
                return false;
            }

            Type scriptType = selectedScript.GetClass();
            if (scriptType == null)
            {
                return false;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(scriptType))
            {
                return false;
            }

            if (scriptType.IsAbstract)
            {
                return false;
            }

            if (scriptType.IsGenericType)
            {
                return false;
            }

            componentType = scriptType;
            return true;
        }

        private static string BuildCreatedObjectName(Type firstComponentType)
        {
            string baseName = firstComponentType != null ? firstComponentType.Name : "GameObject";
            return string.Concat(CreatedObjectPrefix, baseName);
        }

        private static GameObject GetFirstSelectedSceneGameObject()
        {
            GameObject[] selectedGameObjects = Selection.gameObjects;
            for (int index = 0; index < selectedGameObjects.Length; index++)
            {
                GameObject selectedGameObject = selectedGameObjects[index];
                if (selectedGameObject == null)
                {
                    continue;
                }

                if (EditorUtility.IsPersistent(selectedGameObject))
                {
                    continue;
                }

                Scene selectedScene = selectedGameObject.scene;
                if (!selectedScene.IsValid() || !selectedScene.isLoaded)
                {
                    continue;
                }

                return selectedGameObject;
            }

            return null;
        }

        private static Transform GetFirstSelectedSceneGameObjectTransform()
        {
            GameObject selectedGameObject = GetFirstSelectedSceneGameObject();
            return selectedGameObject != null ? selectedGameObject.transform : null;
        }

        private static void PlaceObjectInCurrentStage(GameObject createdObject)
        {
            if (createdObject == null)
            {
                return;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                SceneManager.MoveGameObjectToScene(createdObject, prefabStage.scene);
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(createdObject, activeScene);
            }
        }

        private static void ApplyParent(Transform createdTransform, Transform parentTransform)
        {
            if (createdTransform == null || parentTransform == null)
            {
                return;
            }

            Undo.SetTransformParent(createdTransform, parentTransform, ParentUndoOperationName);
        }

        private static void ApplyDefaultLocalTransform(Transform createdTransform)
        {
            if (createdTransform == null)
            {
                return;
            }

            Undo.RecordObject(createdTransform, ResetTransformUndoOperationName);
            createdTransform.localPosition = Vector3.zero;
            createdTransform.localRotation = Quaternion.identity;
            createdTransform.localScale = Vector3.one;
            EditorUtility.SetDirty(createdTransform);
        }

        private static int AddComponentsToGameObject(GameObject targetGameObject, List<Type> componentTypes, bool ignoreExistingComponents)
        {
            if (targetGameObject == null || componentTypes == null || componentTypes.Count == 0)
            {
                return 0;
            }

            int addedComponentCount = 0;

            for (int index = 0; index < componentTypes.Count; index++)
            {
                Type componentType = componentTypes[index];
                if (componentType == null)
                {
                    continue;
                }

                if (ignoreExistingComponents && targetGameObject.GetComponent(componentType) != null)
                {
                    continue;
                }

                Undo.AddComponent(targetGameObject, componentType);
                addedComponentCount++;
            }

            return addedComponentCount;
        }

        #endregion

        #region DoTween
        // Not applicable for this editor utility.
        #endregion

        #region Events
        // Not applicable for this editor utility.
        #endregion

        #region Gizmos/Debug
        // Debug output is limited to validation warnings in the Console.
        #endregion

        #region Editor
        // All editor bindings are declared above via MenuItem attributes.
        #endregion
    }

    /*
     * Technical note:
     * - The parent target is the first selected scene GameObject found in Selection.gameObjects.
     * - Project assets that are GameObject-based (for example, prefab assets) are ignored as scene targets.
     * - When a parent is found, the created object uses default local transform values relative to that parent.
     * - When no valid parent is found, the object is created at the root of the active scene.
     * - The "Add to selected" command adds only missing component types to the first selected scene GameObject.
     */
}
