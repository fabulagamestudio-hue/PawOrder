using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Iung.Tools.Shared.Editor.Creation
{
    // AI GUIDANCE:
    // - When to use: when two scene objects are selected and the active object should push serialized values into the other one.
    // - Prefer: this tool for Inspector-driven value transfer between matching components and serialized fields.
    // - Avoid: using it with duplicated component types on either object, asset objects, or incompatible property structures.
    // - Reason: the workflow keeps copy operations explicit, undo-safe, and aligned with the selected Inspector context.
    [InitializeOnLoad]
    internal static class PropertyValueTransferTool
    {
        #region Fields

        private const string CopyPropertyMenuLabel = "Copy Value (A -> B)";
        private const string CopyComponentMenuLabel = "CONTEXT/Component/Copy Value (A -> B)";
        private const string CopyPropertyUndoOperationName = "Copy Serialized Property Value";
        private const string CopyComponentUndoOperationName = "Copy Component Serialized Values";
        private const string DuplicateComponentDialogTitle = "Copy Value (A -> B)";
        private const string DuplicateComponentDialogMessage = "The copy operation was cancelled because the source or destination object contains multiple components of the same type. Remove the ambiguity before copying values.";
        private const string SelectionDialogTitle = "Copy Value (A -> B)";
        private const string SelectionDialogMessage = "Select exactly two scene objects. The first selected object is used as source (A) and the second selected object is used as destination (B).";
        private static readonly int[] SelectionOrderBuffer = new int[2];
        private static int _selectionOrderCount;
        private static int[] _lastSelectionInstanceIds = Array.Empty<int>();
        private const string MissingComponentDialogTitle = "Copy Value (A -> B)";
        private const string MissingComponentDialogMessage = "The destination object does not contain a matching component for the selected copy operation.";
        private const string UnsupportedComponentDialogTitle = "Copy Value (A -> B)";
        private const string UnsupportedComponentDialogMessage = "The selected component cannot be copied because it is not a scene component on a valid GameObject.";
        private const string UnsupportedPropertyDialogTitle = "Copy Value (A -> B)";
        private const string UnsupportedPropertyDialogMessage = "The selected property cannot be copied because the Inspector context could not be resolved into a unique shared component.";
        private const string IncompatiblePropertyDialogTitle = "Copy Value (A -> B)";
        private const string IncompatiblePropertyDialogMessage = "The destination component does not expose a compatible serialized property for the selected copy operation.";

        #endregion

        #region Properties
        // No editor properties required.
        #endregion

        #region Unity Messages

        static PropertyValueTransferTool()
        {
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
            Selection.selectionChanged += OnSelectionChanged;
            RefreshSelectionOrder();
        }

        #endregion

        #region Public API

        [MenuItem(CopyComponentMenuLabel, false, 1510)]
        private static void CopyComponentValues(MenuCommand command)
        {
            if (!TryBuildComponentTransferContext(command.context, out ComponentTransferContext transferContext, showDialogs: true))
            {
                return;
            }

            CopySerializedComponent(transferContext);
        }

        [MenuItem(CopyComponentMenuLabel, true)]
        private static bool ValidateCopyComponentValues(MenuCommand command)
        {
            return TryBuildComponentTransferContext(command.context, out _, showDialogs: false);
        }

        #endregion

        #region Internal Logic

        private static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
        {
            if (!TryCreatePropertyMenuSnapshot(property, out PropertyMenuSnapshot snapshot, showDialogs: false))
            {
                return;
            }

            LogPropertySnapshot("MenuCreated", snapshot);
            AddPropertyMenuItems(menu, snapshot);
        }

        private static void ExecuteCopySerializedProperty(PropertyMenuSnapshot snapshot)
        {
            LogPropertySnapshot("ExecuteRequested", snapshot);
            if (!TryBuildPropertyTransferContext(snapshot, out PropertyTransferContext transferContext, showDialogs: true))
            {
                return;
            }

            CopySerializedProperty(transferContext);
        }

        private static void AddPropertyMenuItems(GenericMenu menu, PropertyMenuSnapshot snapshot)
        {
            if (TryAddTransformAxisMenuItems(menu, snapshot))
            {
                return;
            }

            string propertyMenuLabel = BuildPropertyMenuLabel(snapshot);
            menu.AddItem(new GUIContent(propertyMenuLabel), false, () => ExecuteCopySerializedProperty(snapshot));
        }

        private static bool TryAddTransformAxisMenuItems(GenericMenu menu, PropertyMenuSnapshot snapshot)
        {
            if (!IsTransformVector3Property(snapshot))
            {
                return false;
            }

            AddTransformAxisMenuItem(menu, snapshot, "x", "X");
            AddTransformAxisMenuItem(menu, snapshot, "y", "Y");
            AddTransformAxisMenuItem(menu, snapshot, "z", "Z");
            menu.AddSeparator(string.Empty);

            string fullVectorMenuLabel = BuildPropertyMenuLabel(snapshot);
            menu.AddItem(new GUIContent(fullVectorMenuLabel), false, () => ExecuteCopySerializedProperty(snapshot));
            return true;
        }

        private static void AddTransformAxisMenuItem(GenericMenu menu, PropertyMenuSnapshot snapshot, string axisSuffix, string axisDisplayName)
        {
            PropertyMenuSnapshot axisSnapshot = snapshot.WithAxis(axisSuffix, axisDisplayName);
            string axisMenuLabel = BuildPropertyMenuLabel(axisSnapshot);
            menu.AddItem(new GUIContent(axisMenuLabel), false, () => ExecuteCopySerializedProperty(axisSnapshot));
        }

        private static void OnSelectionChanged()
        {
            RefreshSelectionOrder();
        }

        private static void RefreshSelectionOrder()
        {
            GameObject[] selectedGameObjects = Selection.gameObjects;
            int[] currentSelectionInstanceIds = new int[selectedGameObjects.Length];
            int currentSelectionCount = 0;

            for (int index = 0; index < selectedGameObjects.Length; index++)
            {
                GameObject selectedGameObject = selectedGameObjects[index];
                if (!IsValidSceneGameObject(selectedGameObject))
                {
                    continue;
                }

                currentSelectionInstanceIds[currentSelectionCount] = selectedGameObject.GetInstanceID();
                currentSelectionCount++;
            }

            if (currentSelectionCount == 0)
            {
                _selectionOrderCount = 0;
                _lastSelectionInstanceIds = Array.Empty<int>();
                return;
            }

            RemoveDeselectedObjects(currentSelectionInstanceIds, currentSelectionCount);
            AppendNewlySelectedObjects(currentSelectionInstanceIds, currentSelectionCount);
            TrimSelectionOrderToCurrentSelection(currentSelectionInstanceIds, currentSelectionCount);
            _lastSelectionInstanceIds = CopySelectionInstanceIds(currentSelectionInstanceIds, currentSelectionCount);
        }

        private static void RemoveDeselectedObjects(int[] currentSelectionInstanceIds, int currentSelectionCount)
        {
            int writeIndex = 0;
            for (int index = 0; index < _selectionOrderCount; index++)
            {
                int orderedInstanceId = SelectionOrderBuffer[index];
                if (ContainsInstanceId(currentSelectionInstanceIds, currentSelectionCount, orderedInstanceId))
                {
                    SelectionOrderBuffer[writeIndex] = orderedInstanceId;
                    writeIndex++;
                }
            }

            _selectionOrderCount = writeIndex;
        }

        private static void AppendNewlySelectedObjects(int[] currentSelectionInstanceIds, int currentSelectionCount)
        {
            for (int index = 0; index < currentSelectionCount; index++)
            {
                int currentSelectionInstanceId = currentSelectionInstanceIds[index];
                if (ContainsInstanceId(_lastSelectionInstanceIds, _lastSelectionInstanceIds.Length, currentSelectionInstanceId))
                {
                    continue;
                }

                PushSelectionInstanceId(currentSelectionInstanceId);
            }
        }

        private static void TrimSelectionOrderToCurrentSelection(int[] currentSelectionInstanceIds, int currentSelectionCount)
        {
            if (_selectionOrderCount == 0)
            {
                for (int index = 0; index < currentSelectionCount && index < SelectionOrderBuffer.Length; index++)
                {
                    SelectionOrderBuffer[index] = currentSelectionInstanceIds[index];
                }

                _selectionOrderCount = Mathf.Min(currentSelectionCount, SelectionOrderBuffer.Length);
                return;
            }

            if (_selectionOrderCount > currentSelectionCount)
            {
                _selectionOrderCount = currentSelectionCount;
            }
        }

        private static void PushSelectionInstanceId(int instanceId)
        {
            if (_selectionOrderCount < SelectionOrderBuffer.Length)
            {
                SelectionOrderBuffer[_selectionOrderCount] = instanceId;
                _selectionOrderCount++;
                return;
            }

            SelectionOrderBuffer[0] = SelectionOrderBuffer[1];
            SelectionOrderBuffer[1] = instanceId;
        }

        private static int[] CopySelectionInstanceIds(int[] selectionInstanceIds, int selectionCount)
        {
            int[] copiedSelectionInstanceIds = new int[selectionCount];
            Array.Copy(selectionInstanceIds, copiedSelectionInstanceIds, selectionCount);
            return copiedSelectionInstanceIds;
        }

        private static bool ContainsInstanceId(int[] instanceIds, int count, int instanceId)
        {
            for (int index = 0; index < count; index++)
            {
                if (instanceIds[index] == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetOrderedSelectionContext(out SelectionContext selectionContext)
        {
            selectionContext = default;
            if (_selectionOrderCount != 2)
            {
                return false;
            }

            GameObject sourceGameObject = EditorUtility.InstanceIDToObject(SelectionOrderBuffer[0]) as GameObject;
            GameObject destinationGameObject = EditorUtility.InstanceIDToObject(SelectionOrderBuffer[1]) as GameObject;
            if (!IsValidSceneGameObject(sourceGameObject) || !IsValidSceneGameObject(destinationGameObject))
            {
                return false;
            }

            selectionContext = new SelectionContext(sourceGameObject, destinationGameObject);
            return true;
        }

        private static bool IsTransformVector3Property(PropertyMenuSnapshot snapshot)
        {
            if (!string.Equals(snapshot.ComponentTypeName, typeof(Transform).AssemblyQualifiedName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(snapshot.PropertyTypeName, SerializedPropertyType.Vector3.ToString(), StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(snapshot.PropertyPath, "m_LocalPosition", StringComparison.Ordinal) ||
                   string.Equals(snapshot.PropertyPath, "m_LocalScale", StringComparison.Ordinal);
        }

        private static string BuildPropertyMenuLabel(PropertyMenuSnapshot snapshot)
        {
            string displayName = string.IsNullOrWhiteSpace(snapshot.DisplayName) ? snapshot.PropertyPath : snapshot.DisplayName;
            string sourceName = snapshot.SourceObjectName;
            string destinationName = snapshot.DestinationObjectName;
            return $"Copy '{displayName}' from '{sourceName}' to '{destinationName}'";
        }

        private static bool TryCreatePropertyMenuSnapshot(SerializedProperty property, out PropertyMenuSnapshot snapshot, bool showDialogs)
        {
            snapshot = default;
            if (property == null)
            {
                return false;
            }

            if (!TryBuildSelectionContext(out SelectionContext selectionContext, showDialogs))
            {
                return false;
            }

            if (!TryResolveSourceComponentForProperty(property, selectionContext, out Component sourceComponent, showDialogs))
            {
                return false;
            }

            if (!TryGetMatchingComponent(selectionContext, sourceComponent.GetType(), out _, showDialogs))
            {
                return false;
            }

            snapshot = new PropertyMenuSnapshot(
                selectionContext.SourceGameObject.GetInstanceID(),
                selectionContext.DestinationGameObject.GetInstanceID(),
                sourceComponent.GetType().AssemblyQualifiedName,
                property.propertyPath,
                property.displayName,
                property.propertyType.ToString());
            return true;
        }

        private static bool TryBuildPropertyTransferContext(PropertyMenuSnapshot snapshot, out PropertyTransferContext transferContext, bool showDialogs)
        {
            transferContext = default;
            if (!TryRestoreSelectionContext(snapshot, out SelectionContext selectionContext, showDialogs))
            {
                return false;
            }

            Type componentType = ResolveComponentType(snapshot.ComponentTypeName);
            if (componentType == null)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(UnsupportedPropertyDialogTitle, UnsupportedPropertyDialogMessage, "OK");
                }

                return false;
            }

            if (!TryGetUniqueComponent(selectionContext.SourceGameObject, componentType, out Component sourceComponent, showDialogs, allowMissing: false))
            {
                return false;
            }

            if (!TryGetMatchingComponent(selectionContext, componentType, out Component targetComponent, showDialogs))
            {
                return false;
            }

            SerializedObject sourceSerializedObject = new SerializedObject(sourceComponent);
            SerializedObject targetSerializedObject = new SerializedObject(targetComponent);
            sourceSerializedObject.UpdateIfRequiredOrScript();
            targetSerializedObject.UpdateIfRequiredOrScript();

            SerializedProperty sourceProperty = sourceSerializedObject.FindProperty(snapshot.PropertyPath);
            SerializedProperty targetProperty = targetSerializedObject.FindProperty(snapshot.PropertyPath);
            if (sourceProperty == null || targetProperty == null)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(IncompatiblePropertyDialogTitle, IncompatiblePropertyDialogMessage, "OK");
                }

                return false;
            }

            if (!ArePropertiesCompatible(sourceProperty, targetProperty))
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(IncompatiblePropertyDialogTitle, IncompatiblePropertyDialogMessage, "OK");
                }

                return false;
            }

            transferContext = new PropertyTransferContext(selectionContext, sourceComponent, targetComponent, snapshot.PropertyPath, snapshot.DisplayName, snapshot.PropertyTypeName);
            return true;
        }

        private static bool TryResolveSourceComponentForProperty(SerializedProperty property, SelectionContext selectionContext, out Component sourceComponent, bool showDialogs)
        {
            sourceComponent = null;
            if (property?.serializedObject == null)
            {
                return false;
            }

            if (property.serializedObject.targetObject is Component directComponent &&
                IsComponentOnGameObject(directComponent, selectionContext.SourceGameObject))
            {
                sourceComponent = directComponent;
                return true;
            }

            Type componentType = TryResolvePropertyComponentType(property, selectionContext);
            if (componentType == null)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(UnsupportedPropertyDialogTitle, UnsupportedPropertyDialogMessage, "OK");
                }

                return false;
            }

            if (!TryGetUniqueComponent(selectionContext.SourceGameObject, componentType, out sourceComponent, showDialogs, allowMissing: false))
            {
                if (showDialogs && sourceComponent == null)
                {
                    EditorUtility.DisplayDialog(UnsupportedPropertyDialogTitle, UnsupportedPropertyDialogMessage, "OK");
                }

                return false;
            }

            return true;
        }

        private static Type TryResolvePropertyComponentType(SerializedProperty property, SelectionContext selectionContext)
        {
            Object[] targetObjects = property.serializedObject.targetObjects;
            if (targetObjects == null || targetObjects.Length == 0)
            {
                return null;
            }

            Type resolvedComponentType = null;
            for (int index = 0; index < targetObjects.Length; index++)
            {
                if (targetObjects[index] is not Component currentComponent)
                {
                    continue;
                }

                if (!IsComponentOnGameObject(currentComponent, selectionContext.SourceGameObject) &&
                    !IsComponentOnGameObject(currentComponent, selectionContext.DestinationGameObject))
                {
                    continue;
                }

                SerializedObject currentSerializedObject = new SerializedObject(currentComponent);
                SerializedProperty currentProperty = currentSerializedObject.FindProperty(property.propertyPath);
                if (currentProperty == null)
                {
                    continue;
                }

                if (resolvedComponentType == null)
                {
                    resolvedComponentType = currentComponent.GetType();
                    continue;
                }

                if (resolvedComponentType != currentComponent.GetType())
                {
                    return null;
                }
            }

            return resolvedComponentType;
        }

        private static bool TryBuildComponentTransferContext(Object contextObject, out ComponentTransferContext transferContext, bool showDialogs)
        {
            transferContext = default;
            if (contextObject is not Component sourceComponent)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(UnsupportedComponentDialogTitle, UnsupportedComponentDialogMessage, "OK");
                }

                return false;
            }

            if (!TryBuildSelectionContext(out SelectionContext selectionContext, showDialogs))
            {
                return false;
            }

            if (!TryGetMatchingComponent(selectionContext, sourceComponent.GetType(), out Component targetComponent, showDialogs))
            {
                return false;
            }

            if (!IsComponentOnGameObject(sourceComponent, selectionContext.SourceGameObject))
            {
                if (!TryGetUniqueComponent(selectionContext.SourceGameObject, sourceComponent.GetType(), out sourceComponent, showDialogs, allowMissing: false))
                {
                    return false;
                }
            }

            transferContext = new ComponentTransferContext(selectionContext, sourceComponent, targetComponent);
            return true;
        }

        private static bool TryRestoreSelectionContext(PropertyMenuSnapshot snapshot, out SelectionContext selectionContext, bool showDialogs)
        {
            selectionContext = default;
            GameObject sourceGameObject = EditorUtility.InstanceIDToObject(snapshot.SourceGameObjectInstanceId) as GameObject;
            GameObject destinationGameObject = EditorUtility.InstanceIDToObject(snapshot.DestinationGameObjectInstanceId) as GameObject;
            if (!IsValidSceneGameObject(sourceGameObject) || !IsValidSceneGameObject(destinationGameObject))
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(SelectionDialogTitle, SelectionDialogMessage, "OK");
                }

                return false;
            }

            selectionContext = new SelectionContext(sourceGameObject, destinationGameObject);
            return true;
        }

        private static Type ResolveComponentType(string componentTypeName)
        {
            if (string.IsNullOrEmpty(componentTypeName))
            {
                return null;
            }

            return Type.GetType(componentTypeName);
        }

        private static bool TryBuildSelectionContext(out SelectionContext selectionContext, bool showDialogs)
        {
            selectionContext = default;
            GameObject[] selectedGameObjects = Selection.gameObjects;
            if (selectedGameObjects == null || selectedGameObjects.Length != 2)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(SelectionDialogTitle, SelectionDialogMessage, "OK");
                }

                return false;
            }

            RefreshSelectionOrder();
            if (!TryGetOrderedSelectionContext(out selectionContext))
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(SelectionDialogTitle, SelectionDialogMessage, "OK");
                }

                return false;
            }

            return true;
        }

        private static bool TryGetMatchingComponent(SelectionContext selectionContext, Type componentType, out Component targetComponent, bool showDialogs)
        {
            targetComponent = null;
            if (componentType == null)
            {
                return false;
            }

            if (!TryGetUniqueComponent(selectionContext.SourceGameObject, componentType, out _, showDialogs, allowMissing: false))
            {
                return false;
            }

            if (!TryGetUniqueComponent(selectionContext.DestinationGameObject, componentType, out targetComponent, showDialogs, allowMissing: true))
            {
                return false;
            }

            if (targetComponent == null)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(MissingComponentDialogTitle, MissingComponentDialogMessage, "OK");
                }

                return false;
            }

            return true;
        }

        private static bool TryGetUniqueComponent(GameObject gameObject, Type componentType, out Component component, bool showDialogs, bool allowMissing)
        {
            component = null;
            if (gameObject == null || componentType == null)
            {
                return false;
            }

            Component[] components = gameObject.GetComponents(componentType);
            if (components.Length > 1)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(DuplicateComponentDialogTitle, DuplicateComponentDialogMessage, "OK");
                }

                return false;
            }

            if (components.Length == 0)
            {
                return allowMissing;
            }

            component = components[0];
            return true;
        }

        private static bool IsComponentOnGameObject(Component component, GameObject gameObject)
        {
            return component != null && gameObject != null && component.gameObject == gameObject;
        }

        private static bool IsValidSceneGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (EditorUtility.IsPersistent(gameObject))
            {
                return false;
            }

            Scene scene = gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static bool ArePropertiesCompatible(SerializedProperty sourceProperty, SerializedProperty targetProperty)
        {
            if (sourceProperty == null || targetProperty == null)
            {
                return false;
            }

            if (sourceProperty.propertyType != targetProperty.propertyType)
            {
                return false;
            }

            return sourceProperty.isArray == targetProperty.isArray;
        }

        private static void CopySerializedProperty(PropertyTransferContext transferContext)
        {
            if (transferContext.SourceComponent == null || transferContext.TargetComponent == null)
            {
                return;
            }

            LogTransferExecution("CopySerializedProperty", transferContext, transferContext.PropertyPath);
            if (TryCopyTransformAxisProperty(transferContext))
            {
                return;
            }

            SerializedObject sourceSerializedObject = new SerializedObject(transferContext.SourceComponent);
            SerializedObject targetSerializedObject = new SerializedObject(transferContext.TargetComponent);
            sourceSerializedObject.UpdateIfRequiredOrScript();
            targetSerializedObject.UpdateIfRequiredOrScript();

            SerializedProperty sourceProperty = sourceSerializedObject.FindProperty(transferContext.PropertyPath);
            SerializedProperty targetProperty = targetSerializedObject.FindProperty(transferContext.PropertyPath);
            if (sourceProperty == null || targetProperty == null)
            {
                return;
            }

            Undo.RecordObject(transferContext.TargetComponent, CopyPropertyUndoOperationName);
            CopyPropertyValue(sourceProperty, targetProperty);
            targetSerializedObject.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(transferContext.TargetComponent);
            MarkDirty(transferContext.TargetComponent.gameObject.scene, transferContext.TargetComponent);
        }


        private static bool TryCopyTransformAxisProperty(PropertyTransferContext transferContext)
        {
            if (transferContext.SourceComponent is not Transform sourceTransform || transferContext.TargetComponent is not Transform targetTransform)
            {
                return false;
            }

            string axisName = ExtractAxisName(transferContext.PropertyPath);
            if (string.IsNullOrEmpty(axisName))
            {
                return false;
            }

            Undo.RecordObject(targetTransform, CopyPropertyUndoOperationName);

            switch (transferContext.PropertyPath)
            {
                case "m_LocalPosition.x":
                case "m_LocalPosition.y":
                case "m_LocalPosition.z":
                {
                    Vector3 targetValue = targetTransform.localPosition;
                    Vector3 sourceValue = sourceTransform.localPosition;
                    LogTransferExecution("TransformAxis.LocalPosition", transferContext, transferContext.PropertyPath);
                    SetVector3Axis(ref targetValue, axisName, sourceValue);
                    targetTransform.localPosition = targetValue;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetTransform);
                    MarkDirty(targetTransform.gameObject.scene, targetTransform);
                    return true;
                }
                case "m_LocalScale.x":
                case "m_LocalScale.y":
                case "m_LocalScale.z":
                {
                    Vector3 targetValue = targetTransform.localScale;
                    Vector3 sourceValue = sourceTransform.localScale;
                    LogTransferExecution("TransformAxis.LocalScale", transferContext, transferContext.PropertyPath);
                    SetVector3Axis(ref targetValue, axisName, sourceValue);
                    targetTransform.localScale = targetValue;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetTransform);
                    MarkDirty(targetTransform.gameObject.scene, targetTransform);
                    return true;
                }
                default:
                    return false;
            }
        }

        private static void CopySerializedComponent(ComponentTransferContext transferContext)
        {
            if (transferContext.SourceComponent == null || transferContext.TargetComponent == null)
            {
                return;
            }

            SerializedObject sourceSerializedObject = new SerializedObject(transferContext.SourceComponent);
            SerializedObject targetSerializedObject = new SerializedObject(transferContext.TargetComponent);
            SerializedProperty iterator = sourceSerializedObject.GetIterator();
            bool enterChildren = true;

            Undo.RecordObject(transferContext.TargetComponent, CopyComponentUndoOperationName);

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                SerializedProperty targetProperty = targetSerializedObject.FindProperty(iterator.propertyPath);
                if (targetProperty == null)
                {
                    continue;
                }

                CopyPropertyValue(iterator, targetProperty);
            }

            targetSerializedObject.ApplyModifiedProperties();
            MarkDirty(transferContext.TargetComponent.gameObject.scene, transferContext.TargetComponent);
        }

        private static void CopyPropertyValue(SerializedProperty sourceProperty, SerializedProperty targetProperty)
        {
            if (sourceProperty == null || targetProperty == null)
            {
                return;
            }

            if (TryCopyCompositeAxisValue(sourceProperty, targetProperty))
            {
                return;
            }

            if (sourceProperty.isArray && sourceProperty.propertyType != SerializedPropertyType.String)
            {
                CopyArrayProperty(sourceProperty, targetProperty);
                return;
            }

            switch (sourceProperty.propertyType)
            {
                case SerializedPropertyType.Integer:
                    targetProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.Boolean:
                    targetProperty.boolValue = sourceProperty.boolValue;
                    return;
                case SerializedPropertyType.Float:
                    targetProperty.floatValue = sourceProperty.floatValue;
                    return;
                case SerializedPropertyType.String:
                    targetProperty.stringValue = sourceProperty.stringValue;
                    return;
                case SerializedPropertyType.Color:
                    targetProperty.colorValue = sourceProperty.colorValue;
                    return;
                case SerializedPropertyType.ObjectReference:
                    targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                    return;
                case SerializedPropertyType.LayerMask:
                    targetProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.Enum:
                    targetProperty.enumValueIndex = sourceProperty.enumValueIndex;
                    return;
                case SerializedPropertyType.Vector2:
                    targetProperty.vector2Value = sourceProperty.vector2Value;
                    return;
                case SerializedPropertyType.Vector3:
                    targetProperty.vector3Value = sourceProperty.vector3Value;
                    return;
                case SerializedPropertyType.Vector4:
                    targetProperty.vector4Value = sourceProperty.vector4Value;
                    return;
                case SerializedPropertyType.Rect:
                    targetProperty.rectValue = sourceProperty.rectValue;
                    return;
                case SerializedPropertyType.ArraySize:
                    targetProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.Character:
                    targetProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.AnimationCurve:
                    targetProperty.animationCurveValue = sourceProperty.animationCurveValue;
                    return;
                case SerializedPropertyType.Bounds:
                    targetProperty.boundsValue = sourceProperty.boundsValue;
                    return;
                case SerializedPropertyType.Gradient:
                    targetProperty.gradientValue = sourceProperty.gradientValue;
                    return;
                case SerializedPropertyType.Quaternion:
                    targetProperty.quaternionValue = sourceProperty.quaternionValue;
                    return;
                case SerializedPropertyType.ExposedReference:
                    targetProperty.exposedReferenceValue = sourceProperty.exposedReferenceValue;
                    return;
                case SerializedPropertyType.FixedBufferSize:
                    return;
                case SerializedPropertyType.Vector2Int:
                    targetProperty.vector2IntValue = sourceProperty.vector2IntValue;
                    return;
                case SerializedPropertyType.Vector3Int:
                    targetProperty.vector3IntValue = sourceProperty.vector3IntValue;
                    return;
                case SerializedPropertyType.RectInt:
                    targetProperty.rectIntValue = sourceProperty.rectIntValue;
                    return;
                case SerializedPropertyType.BoundsInt:
                    targetProperty.boundsIntValue = sourceProperty.boundsIntValue;
                    return;
                case SerializedPropertyType.ManagedReference:
                    targetProperty.managedReferenceValue = sourceProperty.managedReferenceValue;
                    return;
                case SerializedPropertyType.Hash128:
                    targetProperty.hash128Value = sourceProperty.hash128Value;
                    return;
                case SerializedPropertyType.Generic:
                    CopyGenericProperty(sourceProperty, targetProperty);
                    return;
                default:
                    return;
            }
        }

        private static void CopyArrayProperty(SerializedProperty sourceProperty, SerializedProperty targetProperty)
        {
            targetProperty.arraySize = sourceProperty.arraySize;
            for (int index = 0; index < sourceProperty.arraySize; index++)
            {
                SerializedProperty sourceElement = sourceProperty.GetArrayElementAtIndex(index);
                SerializedProperty targetElement = targetProperty.GetArrayElementAtIndex(index);
                CopyPropertyValue(sourceElement, targetElement);
            }
        }

        private static bool TryCopyCompositeAxisValue(SerializedProperty sourceProperty, SerializedProperty targetProperty)
        {
            if (sourceProperty.propertyType != SerializedPropertyType.Float && sourceProperty.propertyType != SerializedPropertyType.Integer)
            {
                return false;
            }

            string axisName = ExtractAxisName(sourceProperty.propertyPath);
            if (string.IsNullOrEmpty(axisName))
            {
                return false;
            }

            string parentPath = ExtractParentPath(sourceProperty.propertyPath);
            if (string.IsNullOrEmpty(parentPath))
            {
                return false;
            }

            SerializedProperty sourceParentProperty = sourceProperty.serializedObject.FindProperty(parentPath);
            SerializedProperty targetParentProperty = targetProperty.serializedObject.FindProperty(parentPath);
            if (sourceParentProperty == null || targetParentProperty == null)
            {
                return false;
            }

            if (sourceParentProperty.propertyType != targetParentProperty.propertyType)
            {
                return false;
            }

            switch (sourceParentProperty.propertyType)
            {
                case SerializedPropertyType.Vector2:
                    Vector2 sourceVector2 = sourceParentProperty.vector2Value;
                    Vector2 targetVector2 = targetParentProperty.vector2Value;
                    SetVector2Axis(ref targetVector2, axisName, sourceVector2);
                    targetParentProperty.vector2Value = targetVector2;
                    return true;
                case SerializedPropertyType.Vector3:
                    Vector3 sourceVector3 = sourceParentProperty.vector3Value;
                    Vector3 targetVector3 = targetParentProperty.vector3Value;
                    SetVector3Axis(ref targetVector3, axisName, sourceVector3);
                    targetParentProperty.vector3Value = targetVector3;
                    return true;
                case SerializedPropertyType.Vector4:
                    Vector4 sourceVector4 = sourceParentProperty.vector4Value;
                    Vector4 targetVector4 = targetParentProperty.vector4Value;
                    SetVector4Axis(ref targetVector4, axisName, sourceVector4);
                    targetParentProperty.vector4Value = targetVector4;
                    return true;
                case SerializedPropertyType.Vector2Int:
                    Vector2Int sourceVector2Int = sourceParentProperty.vector2IntValue;
                    Vector2Int targetVector2Int = targetParentProperty.vector2IntValue;
                    SetVector2IntAxis(ref targetVector2Int, axisName, sourceVector2Int);
                    targetParentProperty.vector2IntValue = targetVector2Int;
                    return true;
                case SerializedPropertyType.Vector3Int:
                    Vector3Int sourceVector3Int = sourceParentProperty.vector3IntValue;
                    Vector3Int targetVector3Int = targetParentProperty.vector3IntValue;
                    SetVector3IntAxis(ref targetVector3Int, axisName, sourceVector3Int);
                    targetParentProperty.vector3IntValue = targetVector3Int;
                    return true;
                default:
                    return false;
            }
        }

        private static string ExtractParentPath(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return string.Empty;
            }

            int lastDotIndex = propertyPath.LastIndexOf('.');
            if (lastDotIndex <= 0)
            {
                return string.Empty;
            }

            return propertyPath.Substring(0, lastDotIndex);
        }

        private static string ExtractAxisName(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return string.Empty;
            }

            int lastDotIndex = propertyPath.LastIndexOf('.');
            if (lastDotIndex < 0 || lastDotIndex >= propertyPath.Length - 1)
            {
                return string.Empty;
            }

            return propertyPath.Substring(lastDotIndex + 1);
        }

        private static void SetVector2Axis(ref Vector2 targetValue, string axisName, Vector2 sourceValue)
        {
            switch (axisName)
            {
                case "x":
                    targetValue.x = sourceValue.x;
                    break;
                case "y":
                    targetValue.y = sourceValue.y;
                    break;
            }
        }

        private static void SetVector3Axis(ref Vector3 targetValue, string axisName, Vector3 sourceValue)
        {
            switch (axisName)
            {
                case "x":
                    targetValue.x = sourceValue.x;
                    break;
                case "y":
                    targetValue.y = sourceValue.y;
                    break;
                case "z":
                    targetValue.z = sourceValue.z;
                    break;
            }
        }

        private static void SetVector4Axis(ref Vector4 targetValue, string axisName, Vector4 sourceValue)
        {
            switch (axisName)
            {
                case "x":
                    targetValue.x = sourceValue.x;
                    break;
                case "y":
                    targetValue.y = sourceValue.y;
                    break;
                case "z":
                    targetValue.z = sourceValue.z;
                    break;
                case "w":
                    targetValue.w = sourceValue.w;
                    break;
            }
        }

        private static void SetVector2IntAxis(ref Vector2Int targetValue, string axisName, Vector2Int sourceValue)
        {
            switch (axisName)
            {
                case "x":
                    targetValue.x = sourceValue.x;
                    break;
                case "y":
                    targetValue.y = sourceValue.y;
                    break;
            }
        }

        private static void SetVector3IntAxis(ref Vector3Int targetValue, string axisName, Vector3Int sourceValue)
        {
            switch (axisName)
            {
                case "x":
                    targetValue.x = sourceValue.x;
                    break;
                case "y":
                    targetValue.y = sourceValue.y;
                    break;
                case "z":
                    targetValue.z = sourceValue.z;
                    break;
            }
        }

        private static void CopyGenericProperty(SerializedProperty sourceProperty, SerializedProperty targetProperty)
        {
            SerializedProperty sourceIterator = sourceProperty.Copy();
            SerializedProperty sourceEndProperty = sourceIterator.GetEndProperty();
            bool enterChildren = true;

            while (sourceIterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(sourceIterator, sourceEndProperty))
            {
                enterChildren = false;
                if (!sourceIterator.propertyPath.StartsWith(sourceProperty.propertyPath))
                {
                    break;
                }

                string relativePath = BuildRelativePropertyPath(sourceProperty.propertyPath, sourceIterator.propertyPath);
                if (string.IsNullOrEmpty(relativePath))
                {
                    continue;
                }

                SerializedProperty targetChildProperty = targetProperty.FindPropertyRelative(relativePath);
                if (targetChildProperty == null)
                {
                    continue;
                }

                CopyPropertyValue(sourceIterator, targetChildProperty);
            }
        }

        private static string BuildRelativePropertyPath(string parentPath, string fullPath)
        {
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(fullPath))
            {
                return string.Empty;
            }

            if (fullPath.Length <= parentPath.Length)
            {
                return string.Empty;
            }

            int relativeStartIndex = parentPath.Length;
            if (fullPath[relativeStartIndex] == '.')
            {
                relativeStartIndex++;
            }

            if (relativeStartIndex >= fullPath.Length)
            {
                return string.Empty;
            }

            return fullPath.Substring(relativeStartIndex);
        }

        private static void LogPropertySnapshot(string stage, PropertyMenuSnapshot snapshot)
        {
            Debug.Log($"[PropertyValueTransferTool] {stage} | Source='{snapshot.SourceObjectName}' | Target='{snapshot.DestinationObjectName}' | Component='{snapshot.ComponentTypeName}' | DisplayName='{snapshot.DisplayName}' | PropertyPath='{snapshot.PropertyPath}' | PropertyType='{snapshot.PropertyTypeName}'");
        }

        private static void LogTransferExecution(string stage, PropertyTransferContext transferContext, string resolvedPropertyPath)
        {
            string sourceObjectName = transferContext.SelectionContext.SourceGameObject != null ? transferContext.SelectionContext.SourceGameObject.name : "<null>";
            string targetObjectName = transferContext.SelectionContext.DestinationGameObject != null ? transferContext.SelectionContext.DestinationGameObject.name : "<null>";
            string sourceComponentName = transferContext.SourceComponent != null ? transferContext.SourceComponent.GetType().FullName : "<null>";
            string targetComponentName = transferContext.TargetComponent != null ? transferContext.TargetComponent.GetType().FullName : "<null>";
            Debug.Log($"[PropertyValueTransferTool] {stage} | Source='{sourceObjectName}' | Target='{targetObjectName}' | SourceComponent='{sourceComponentName}' | TargetComponent='{targetComponentName}' | DisplayName='{transferContext.DisplayName}' | SnapshotPropertyType='{transferContext.PropertyTypeName}' | SnapshotPath='{transferContext.PropertyPath}' | ResolvedPath='{resolvedPropertyPath}'");
        }

        private static void MarkDirty(Scene scene, Object targetObject)
        {
            if (targetObject != null)
            {
                EditorUtility.SetDirty(targetObject);
            }

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private readonly struct SelectionContext
        {
            #region Fields

            public readonly GameObject SourceGameObject;
            public readonly GameObject DestinationGameObject;

            #endregion

            #region Properties
            // No additional properties required.
            #endregion

            #region Unity Messages
            // Not applicable for this editor struct.
            #endregion

            #region Public API

            public SelectionContext(GameObject sourceGameObject, GameObject destinationGameObject)
            {
                SourceGameObject = sourceGameObject;
                DestinationGameObject = destinationGameObject;
            }

            #endregion

            #region Internal Logic
            // Not applicable for this editor struct.
            #endregion

            #region DoTween
            // Not applicable for this editor struct.
            #endregion

            #region Events
            // Not applicable for this editor struct.
            #endregion

            #region Gizmos/Debug
            // Not applicable for this editor struct.
            #endregion

            #region Editor
            // Not applicable for this editor struct.
            #endregion
        }

        private readonly struct PropertyMenuSnapshot
        {
            #region Fields

            public readonly int SourceGameObjectInstanceId;
            public readonly int DestinationGameObjectInstanceId;
            public readonly string ComponentTypeName;
            public readonly string PropertyPath;
            public readonly string DisplayName;
            public readonly string PropertyTypeName;

            #endregion

            #region Properties

            public string SourceObjectName => (EditorUtility.InstanceIDToObject(SourceGameObjectInstanceId) as GameObject)?.name ?? "<missing>";
            public string DestinationObjectName => (EditorUtility.InstanceIDToObject(DestinationGameObjectInstanceId) as GameObject)?.name ?? "<missing>";

            #endregion

            #region Unity Messages
            // Not applicable for this editor struct.
            #endregion

            #region Public API

            public PropertyMenuSnapshot(int sourceGameObjectInstanceId, int destinationGameObjectInstanceId, string componentTypeName, string propertyPath, string displayName, string propertyTypeName)
            {
                SourceGameObjectInstanceId = sourceGameObjectInstanceId;
                DestinationGameObjectInstanceId = destinationGameObjectInstanceId;
                ComponentTypeName = componentTypeName;
                PropertyPath = propertyPath;
                DisplayName = displayName;
                PropertyTypeName = propertyTypeName;
            }

            public PropertyMenuSnapshot WithAxis(string axisSuffix, string axisDisplayName)
            {
                return new PropertyMenuSnapshot(
                    SourceGameObjectInstanceId,
                    DestinationGameObjectInstanceId,
                    ComponentTypeName,
                    $"{PropertyPath}.{axisSuffix}",
                    axisDisplayName,
                    SerializedPropertyType.Float.ToString());
            }

            #endregion

            #region Internal Logic
            // Not applicable for this editor struct.
            #endregion

            #region DoTween
            // Not applicable for this editor struct.
            #endregion

            #region Events
            // Not applicable for this editor struct.
            #endregion

            #region Gizmos/Debug
            // Not applicable for this editor struct.
            #endregion

            #region Editor
            // Not applicable for this editor struct.
            #endregion
        }

        private readonly struct PropertyTransferContext
        {
            #region Fields

            public readonly SelectionContext SelectionContext;
            public readonly Component SourceComponent;
            public readonly Component TargetComponent;
            public readonly string PropertyPath;
            public readonly string DisplayName;
            public readonly string PropertyTypeName;

            #endregion

            #region Properties
            // No additional properties required.
            #endregion

            #region Unity Messages
            // Not applicable for this editor struct.
            #endregion

            #region Public API

            public PropertyTransferContext(SelectionContext selectionContext, Component sourceComponent, Component targetComponent, string propertyPath, string displayName, string propertyTypeName)
            {
                SelectionContext = selectionContext;
                SourceComponent = sourceComponent;
                TargetComponent = targetComponent;
                PropertyPath = propertyPath;
                DisplayName = displayName;
                PropertyTypeName = propertyTypeName;
            }

            #endregion

            #region Internal Logic
            // Not applicable for this editor struct.
            #endregion

            #region DoTween
            // Not applicable for this editor struct.
            #endregion

            #region Events
            // Not applicable for this editor struct.
            #endregion

            #region Gizmos/Debug
            // Not applicable for this editor struct.
            #endregion

            #region Editor
            // Not applicable for this editor struct.
            #endregion
        }

        private readonly struct ComponentTransferContext
        {
            #region Fields

            public readonly SelectionContext SelectionContext;
            public readonly Component SourceComponent;
            public readonly Component TargetComponent;

            #endregion

            #region Properties
            // No additional properties required.
            #endregion

            #region Unity Messages
            // Not applicable for this editor struct.
            #endregion

            #region Public API

            public ComponentTransferContext(SelectionContext selectionContext, Component sourceComponent, Component targetComponent)
            {
                SelectionContext = selectionContext;
                SourceComponent = sourceComponent;
                TargetComponent = targetComponent;
            }

            #endregion

            #region Internal Logic
            // Not applicable for this editor struct.
            #endregion

            #region DoTween
            // Not applicable for this editor struct.
            #endregion

            #region Events
            // Not applicable for this editor struct.
            #endregion

            #region Gizmos/Debug
            // Not applicable for this editor struct.
            #endregion

            #region Editor
            // Not applicable for this editor struct.
            #endregion
        }

        #endregion

        #region DoTween
        // Not applicable for this editor utility.
        #endregion

        #region Events
        // Not applicable for this editor utility.
        #endregion

        #region Gizmos/Debug
        // Debug output is intentionally omitted to keep the action unobtrusive.
        #endregion

        #region Editor
        // All editor bindings are declared above through the property and component contextual menu hooks.
        #endregion
    }

    /*
     * Technical note:
     * - The active scene object is always treated as source (A), while the other selected scene object becomes destination (B).
     * - Property copy resolves the clicked Inspector context back into the active object's unique matching component before reading SerializedProperty.propertyPath.
     * - Component copy iterates through the serialized data and skips m_Script to avoid breaking script references.
     * - Arrays and lists are fully overwritten in size and content, matching the approved behavior for complete serialized transfer.
     * - Ambiguous cases with duplicated component types are blocked and surfaced with an editor dialog to prevent silent mistakes.
     */
}
