using UnityEditor;
using UnityEngine;

namespace Iung.Animation
{
    /// <summary>
    /// Custom drawer for AnimationProperty. Shows core fields and, when applicable,
    /// a toggle to enable Manual Start/End plus the corresponding inputs,
    /// auto-capturing START from the current object state upon enabling.
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimationProperty))]
    public class AnimationProperty_PropertyDrawer : PropertyDrawer
    {
        const string PreviewStateKeyPrefix = "Iung.Animation.Preview.";
        float lineHeight = EditorGUIUtility.singleLineHeight + 2; // line height + spacing

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lines = 1; // header line with foldout + object

            if (property.isExpanded)
            {
                // Base: time/delays + type/preview + eases+idle sync + actions
                lines += 4;

                // Manual range block (only if supported type + toggle on)
                var animType = (AnimationProperty.AnimationType)property.FindPropertyRelative("_animationType").enumValueIndex;
                bool typeSupportsManual =
                    animType == AnimationProperty.AnimationType.Scale ||
                    animType == AnimationProperty.AnimationType.Fade ||
                    animType == AnimationProperty.AnimationType.OutsideScreen_Left ||
                    animType == AnimationProperty.AnimationType.OutsideScreen_Right ||
                    animType == AnimationProperty.AnimationType.OutsideScreen_Up ||
                    animType == AnimationProperty.AnimationType.OutsideScreen_Down;

                if (typeSupportsManual)
                {
                    lines += 1; // toggle line

                    var useManualProp = property.FindPropertyRelative("useManualRange");
                    if (useManualProp.boolValue)
                    {
                        // Fields depend on type
                        if (animType == AnimationProperty.AnimationType.Scale ||
                            animType == AnimationProperty.AnimationType.Fade)
                        {
                            lines += 2; // Start float + End float
                        }
                        else
                        {
                            lines += 2; // Start Vector2 + End Vector2
                        }
                    }
                }
            }

            return lineHeight * lines;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            // --- Header line (foldout + label + object field)
            float foldoutWidth = 10f;
            GUIStyle labelStyle = GUI.skin.label;
            Vector2 textSize = labelStyle.CalcSize(new GUIContent(property.name));

            Rect foldoutRect = new Rect(position.x, position.y, foldoutWidth, lineHeight);
            Rect objectLabelRect = new Rect(position.x + foldoutWidth, position.y, textSize.x + foldoutWidth, lineHeight);
            Rect headerContentRect = new Rect(position.x + foldoutWidth + textSize.x, position.y, position.width - foldoutWidth - textSize.x, lineHeight);

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none);
            if (Event.current.type == EventType.MouseDown && objectLabelRect.Contains(Event.current.mousePosition))
            {
                property.isExpanded = !property.isExpanded;
                Event.current.Use();
            }
            EditorGUI.LabelField(objectLabelRect, property.name);
            SerializedProperty objectProp = property.FindPropertyRelative("_object");
            SerializedProperty typePropCollapsed = property.FindPropertyRelative("_animationType");
            SerializedProperty timePropCollapsed = property.FindPropertyRelative("time");

            if (property.isExpanded)
            {
                EditorGUI.PropertyField(headerContentRect, objectProp, GUIContent.none);
            }
            else
            {
                // Collapsed row: keep editing available for Object + Type + Time.
                float spacing = 4f;
                float timeWidth = 60f;
                float typeWidth = 140f;
                float objectWidth = Mathf.Max(
                    80f,
                    headerContentRect.width - typeWidth - timeWidth - (spacing * 2));

                Rect objectRect = new Rect(
                    headerContentRect.x,
                    headerContentRect.y,
                    objectWidth,
                    headerContentRect.height);
                Rect typeRect = new Rect(
                    objectRect.xMax + spacing,
                    headerContentRect.y,
                    typeWidth,
                    headerContentRect.height);
                Rect timeRect = new Rect(
                    typeRect.xMax + spacing,
                    headerContentRect.y,
                    Mathf.Max(40f, headerContentRect.xMax - (typeRect.xMax + spacing)),
                    headerContentRect.height);

                EditorGUI.PropertyField(objectRect, objectProp, GUIContent.none);
                EditorGUI.PropertyField(typeRect, typePropCollapsed, GUIContent.none);
                EditorGUI.PropertyField(timeRect, timePropCollapsed, GUIContent.none);
            }

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                // Common sub-rect helpers
                float labelWidthStd = 60f;
                float labelWidthLong = 80f;
                float previewToggleWidth = 80f;

                // --- Line: time + delays
                position.y += lineHeight;
                Rect timeLabelRect = new Rect(position.x, position.y, labelWidthStd, lineHeight);
                Rect timeRect = new Rect(position.x + labelWidthStd, position.y, (position.width - labelWidthLong - labelWidthStd * 2) / 3, lineHeight);

                Rect delayLabelRect = new Rect(position.x + timeRect.width + labelWidthStd, position.y, labelWidthStd, lineHeight);
                Rect delayRect = new Rect(position.x + labelWidthStd + timeRect.width + labelWidthStd, position.y, (position.width - labelWidthLong - labelWidthStd * 2) / 3, lineHeight);

                Rect delayHideLabelRect = new Rect(position.x + 2 * timeRect.width + 2 * labelWidthStd, position.y, labelWidthLong, lineHeight);
                Rect delayHideRect = new Rect(position.x + 2 * timeRect.width + 2 * labelWidthStd + labelWidthLong, position.y, (position.width - labelWidthLong - labelWidthStd * 2) / 3, lineHeight);

                EditorGUI.LabelField(timeLabelRect, "Time");
                EditorGUI.PropertyField(timeRect, property.FindPropertyRelative("time"), GUIContent.none);
                EditorGUI.LabelField(delayLabelRect, "Delay");
                EditorGUI.PropertyField(delayRect, property.FindPropertyRelative("delay"), GUIContent.none);
                EditorGUI.LabelField(delayHideLabelRect, "Hide Delay");
                EditorGUI.PropertyField(delayHideRect, property.FindPropertyRelative("delayHide"), GUIContent.none);

                // --- Line: type + preview toggle
                position.y += lineHeight;
                Rect typeLabelRect = new Rect(position.x, position.y, labelWidthStd, lineHeight);
                Rect animationTypeRect = new Rect(position.x + labelWidthStd, position.y, position.width - labelWidthStd - previewToggleWidth, lineHeight);
                Rect previewToggleRect = new Rect(position.x + position.width - previewToggleWidth, position.y, previewToggleWidth, lineHeight);

                EditorGUI.LabelField(typeLabelRect, "Type");
                var typeProp = property.FindPropertyRelative("_animationType");
                EditorGUI.PropertyField(animationTypeRect, typeProp, GUIContent.none);

                AnimationGlobalSettings settings = AnimationSettingsService.GetOrCreateSettings();
                string previewStateKey = GetPreviewStateKey(property);
                bool previewDefault = settings != null && settings.PreviewMovementByDefault;
                bool previewState = SessionState.GetBool(previewStateKey, previewDefault);
                bool showAnimationPreview = EditorGUI.ToggleLeft(previewToggleRect, "Preview", previewState);
                if (showAnimationPreview != previewState)
                {
                    SessionState.SetBool(previewStateKey, showAnimationPreview);
                    AnimationProperty_ScenePreview.MarkDirtyAndRepaint();
                }

                // --- Line: eases + idle sync
                position.y += lineHeight;
                float thirdWidth = position.width / 3f;
                Rect easeInLabelRect = new Rect(position.x, position.y, 55f, lineHeight);
                Rect easeInRect = new Rect(position.x + 55f, position.y, thirdWidth - 55f, lineHeight);
                Rect easeOutLabelRect = new Rect(position.x + thirdWidth, position.y, 60f, lineHeight);
                Rect easeOutRect = new Rect(position.x + thirdWidth + 60f, position.y, thirdWidth - 60f, lineHeight);
                Rect idleSyncLabelRect = new Rect(position.x + (thirdWidth * 2f), position.y, 65f, lineHeight);
                Rect idleSyncRect = new Rect(position.x + (thirdWidth * 2f) + 65f, position.y, thirdWidth - 65f, lineHeight);

                EditorGUI.LabelField(easeInLabelRect, "Ease In");
                EditorGUI.PropertyField(easeInRect, property.FindPropertyRelative("_easeIn"), GUIContent.none);
                EditorGUI.LabelField(easeOutLabelRect, "Ease Out");
                EditorGUI.PropertyField(easeOutRect, property.FindPropertyRelative("_easeOut"), GUIContent.none);
                EditorGUI.LabelField(idleSyncLabelRect, "Idle Sync");
                EditorGUI.PropertyField(idleSyncRect, property.FindPropertyRelative("idleSyncOptions"), GUIContent.none);

                position.y += lineHeight;
                Rect applyDefaultsRect = new Rect(position.x, position.y, position.width, lineHeight);
                if (GUI.Button(applyDefaultsRect, "Apply Global Defaults"))
                {
                    AnimationGlobalSettings applySettings = AnimationSettingsService.GetOrCreateSettings();
                    if (AnimationSettingsApplyService.ApplyDefaultsToAnimationProperty(property, applySettings))
                    {
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }

                // --- Manual Range (only for supported types)
                var animType = (AnimationProperty.AnimationType)typeProp.enumValueIndex;
                bool typeSupportsManual =
                    animType == AnimationProperty.AnimationType.Scale ||
                    animType == AnimationProperty.AnimationType.Fade ||
                    animType == AnimationProperty.AnimationType.OutsideScreen_Left ||
                    animType == AnimationProperty.AnimationType.OutsideScreen_Right ||
                    animType == AnimationProperty.AnimationType.OutsideScreen_Up ||
                    animType == AnimationProperty.AnimationType.OutsideScreen_Down;

                if (typeSupportsManual)
                {
                    position.y += lineHeight;

                    var useManualProp = property.FindPropertyRelative("useManualRange");
                    bool oldUse = useManualProp.boolValue;

                    // Toggle Manual
                    Rect toggleRect = new Rect(position.x, position.y, position.width, lineHeight);
                    bool newUse = EditorGUI.ToggleLeft(toggleRect, "Manual Start/End", oldUse);
                    if (newUse != oldUse)
                    {
                        useManualProp.boolValue = newUse;

                        // On enabling: capture START from current object state
                        if (newUse)
                        {
                            CaptureManualStartFromCurrent(property, animType);
                            // Set END default to START initially
                            if (animType == AnimationProperty.AnimationType.Scale || animType == AnimationProperty.AnimationType.Fade)
                            {
                                var startF = property.FindPropertyRelative("manualStartFloat").floatValue;
                                property.FindPropertyRelative("manualEndFloat").floatValue = startF;
                            }
                            else
                            {
                                var startV = property.FindPropertyRelative("manualStartPos").vector2Value;
                                property.FindPropertyRelative("manualEndPos").vector2Value = startV;
                            }
                        }
                    }

                    if (useManualProp.boolValue)
                    {
                        position.y += lineHeight;

                        if (animType == AnimationProperty.AnimationType.Scale || animType == AnimationProperty.AnimationType.Fade)
                        {
                            // Start float
                            Rect sLabel = new Rect(position.x, position.y, 80f, lineHeight);
                            Rect sField = new Rect(position.x + 80f, position.y, (position.width - 160f) / 2, lineHeight);
                            EditorGUI.LabelField(sLabel, "Start");
                            EditorGUI.PropertyField(sField, property.FindPropertyRelative("manualStartFloat"), GUIContent.none);

                            // End float
                            Rect eLabel = new Rect(position.x + 80f + sField.width, position.y, 80f, lineHeight);
                            Rect eField = new Rect(position.x + 160f + sField.width, position.y, (position.width - 160f) / 2, lineHeight);
                            EditorGUI.LabelField(eLabel, "End");
                            EditorGUI.PropertyField(eField, property.FindPropertyRelative("manualEndFloat"), GUIContent.none);
                        }
                        else
                        {
                            // Start Vector2
                            Rect sLabel = new Rect(position.x, position.y, 80f, lineHeight);
                            Rect sField = new Rect(position.x + 80f, position.y, (position.width - 160f) / 2, lineHeight);
                            EditorGUI.LabelField(sLabel, "Start");
                            EditorGUI.PropertyField(sField, property.FindPropertyRelative("manualStartPos"), GUIContent.none);

                            // End Vector2
                            Rect eLabel = new Rect(position.x + 80f + sField.width, position.y, 80f, lineHeight);
                            Rect eField = new Rect(position.x + 160f + sField.width, position.y, (position.width - 160f) / 2, lineHeight);
                            EditorGUI.LabelField(eLabel, "End");
                            EditorGUI.PropertyField(eField, property.FindPropertyRelative("manualEndPos"), GUIContent.none);
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                AnimationPropertyHierarchyIconCache.MarkDirtyAndRepaint();
            }

            EditorGUI.EndProperty();
        }

        internal static string GetPreviewStateKey(SerializedProperty property)
        {
            Object targetObject = property != null && property.serializedObject != null
                ? property.serializedObject.targetObject
                : null;
            string propertyPath = property != null ? property.propertyPath : string.Empty;
            return GetPreviewStateKey(targetObject, propertyPath);
        }

        internal static string GetPreviewStateKey(Object targetObject, string propertyPath)
        {
            string targetPath = targetObject != null
                ? targetObject.GetInstanceID().ToString()
                : "NoTarget";

            return PreviewStateKeyPrefix + targetPath + "." + propertyPath;
        }

        internal static bool SupportsHidePreview(SerializedProperty property)
        {
            var animationType = (AnimationProperty.AnimationType)property.FindPropertyRelative("_animationType").enumValueIndex;
            return animationType == AnimationProperty.AnimationType.OutsideScreen_Left ||
                   animationType == AnimationProperty.AnimationType.OutsideScreen_Right ||
                   animationType == AnimationProperty.AnimationType.OutsideScreen_Up ||
                   animationType == AnimationProperty.AnimationType.OutsideScreen_Down ||
                   animationType == AnimationProperty.AnimationType.RectTween;
        }

        internal static void DrawHidePreviewInScene(SerializedProperty property)
        {
            var targetObject = property.FindPropertyRelative("_object").objectReferenceValue as GameObject;
            if (targetObject == null)
                return;

            var rectTransform = targetObject.GetComponent<RectTransform>();
            if (rectTransform == null)
                return;

            if (!TryGetHidePreviewState(property, rectTransform, out var previewCenter, out var previewSize, out var previewRotation))
                return;

            Color previousColor = Handles.color;
            Handles.color = new Color(1f, 0.2f, 0.2f, 0.95f);
            DrawWorldRect(previewCenter, previewRotation, previewSize);
            Handles.color = previousColor;
        }

        // Preview path: compute destination in local UI space, then draw with current world size.
        static bool TryGetHidePreviewState(SerializedProperty property, RectTransform rectTransform, out Vector3 previewCenter, out Vector2 previewSize, out Quaternion previewRotation)
        {
            previewCenter = rectTransform.TransformPoint(rectTransform.rect.center);
            previewSize = GetCurrentWorldSize(rectTransform);
            previewRotation = rectTransform.rotation;

            var animationType = (AnimationProperty.AnimationType)property.FindPropertyRelative("_animationType").enumValueIndex;
            bool useManualRange = property.FindPropertyRelative("useManualRange").boolValue;

            switch (animationType)
            {
                case AnimationProperty.AnimationType.OutsideScreen_Left:
                case AnimationProperty.AnimationType.OutsideScreen_Right:
                case AnimationProperty.AnimationType.OutsideScreen_Up:
                case AnimationProperty.AnimationType.OutsideScreen_Down:
                    {
                        Vector2 anchoredHidePosition = useManualRange
                            ? property.FindPropertyRelative("manualStartPos").vector2Value
                            : AnimationProperty.CalculateLegacyHideAnchoredPosition(
                                animationType,
                                rectTransform,
                                GetPreviewViewportSize(rectTransform));

                        return TryBuildPreviewFromTemporaryRect(
                            rectTransform,
                            anchoredHidePosition,
                            rectTransform.sizeDelta,
                            rectTransform.localScale,
                            rectTransform.rotation,
                            out previewCenter,
                            out previewSize,
                            out previewRotation);
                    }

                case AnimationProperty.AnimationType.RectTween:
                    {
                        var tweener = rectTransform.GetComponent<UI_RectTransformTweener>();
                        if (tweener == null)
                            return false;

                        RectTransformState hideState = tweener.stateA;
                        return TryBuildPreviewFromTemporaryRect(
                            rectTransform,
                            hideState.anchoredPosition,
                            ResolveRectTweenSizeDelta(rectTransform, hideState),
                            hideState.scale,
                            hideState.rotation,
                            out previewCenter,
                            out previewSize,
                            out previewRotation);
                    }

                default:
                    return false;
            }
        }

        static Vector2 GetPreviewViewportSize(RectTransform rectTransform)
        {
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (canvas.rootCanvas != null)
                    canvas = canvas.rootCanvas;

                Rect pixelRect = canvas.pixelRect;
                if (pixelRect.width > 0f && pixelRect.height > 0f)
                    return pixelRect.size;

                RectTransform canvasRect = canvas.transform as RectTransform;
                if (canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
                    return canvasRect.rect.size;
            }

            return new Vector2(Screen.width, Screen.height);
        }

        static Vector2 GetCurrentWorldSize(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            float width = Vector3.Distance(corners[0], corners[3]);
            float height = Vector3.Distance(corners[0], corners[1]);
            return new Vector2(width, height);
        }

        static Vector2 ResolveRectTweenSizeDelta(RectTransform rectTransform, RectTransformState state)
        {
            if (state.sizeDelta == Vector2.zero)
                return rectTransform.sizeDelta;

            return state.sizeDelta;
        }

        static bool TryBuildPreviewFromTemporaryRect(
            RectTransform source,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Vector3 localScale,
            Quaternion worldRotation,
            out Vector3 previewCenter,
            out Vector2 previewSize,
            out Quaternion previewRotation)
        {
            previewCenter = Vector3.zero;
            previewSize = Vector2.zero;
            previewRotation = worldRotation;

            if (!(source.parent is RectTransform parentRect))
                return false;

            Rect parentLocalRect = parentRect.rect;
            Vector2 anchorRectMin = new Vector2(
                parentLocalRect.xMin + (parentLocalRect.width * source.anchorMin.x),
                parentLocalRect.yMin + (parentLocalRect.height * source.anchorMin.y));
            Vector2 anchorRectMax = new Vector2(
                parentLocalRect.xMin + (parentLocalRect.width * source.anchorMax.x),
                parentLocalRect.yMin + (parentLocalRect.height * source.anchorMax.y));

            Vector2 anchorReference = anchorRectMin + Vector2.Scale(anchorRectMax - anchorRectMin, source.pivot);
            Vector3 pivotLocalPosition = anchorReference + anchoredPosition;
            Vector3 pivotWorldPosition = parentRect.TransformPoint(pivotLocalPosition);

            Vector3 combinedLossyScale = Vector3.Scale(parentRect.lossyScale, localScale);
            Matrix4x4 worldMatrix = Matrix4x4.TRS(pivotWorldPosition, worldRotation, combinedLossyScale);

            Vector2 pivot = source.pivot;
            Vector3 bottomLeft = worldMatrix.MultiplyPoint3x4(new Vector3(-sizeDelta.x * pivot.x, -sizeDelta.y * pivot.y, 0f));
            Vector3 topLeft = worldMatrix.MultiplyPoint3x4(new Vector3(-sizeDelta.x * pivot.x, sizeDelta.y * (1f - pivot.y), 0f));
            Vector3 topRight = worldMatrix.MultiplyPoint3x4(new Vector3(sizeDelta.x * (1f - pivot.x), sizeDelta.y * (1f - pivot.y), 0f));
            Vector3 bottomRight = worldMatrix.MultiplyPoint3x4(new Vector3(sizeDelta.x * (1f - pivot.x), -sizeDelta.y * pivot.y, 0f));

            previewCenter = (bottomLeft + topLeft + topRight + bottomRight) * 0.25f;
            previewSize = new Vector2(
                Vector3.Distance(bottomLeft, bottomRight),
                Vector3.Distance(bottomLeft, topLeft));
            previewRotation = worldRotation;
            return true;
        }

        static void DrawWorldRect(Vector3 center, Quaternion rotation, Vector2 worldSize)
        {
            Vector3 right = rotation * Vector3.right * (worldSize.x * 0.5f);
            Vector3 up = rotation * Vector3.up * (worldSize.y * 0.5f);

            Vector3 topLeft = center - right + up;
            Vector3 topRight = center + right + up;
            Vector3 bottomRight = center + right - up;
            Vector3 bottomLeft = center - right - up;

            Handles.DrawAAPolyLine(2f, topLeft, topRight, bottomRight, bottomLeft, topLeft);
        }

        /// <summary>
        /// Captures the current object state into manual START fields when the toggle is enabled.
        /// </summary>
        private void CaptureManualStartFromCurrent(SerializedProperty property, AnimationProperty.AnimationType type)
        {
            var go = property.FindPropertyRelative("_object").objectReferenceValue as GameObject;
            if (go == null) return;

            switch (type)
            {
                case AnimationProperty.AnimationType.Scale:
                    {
                        float s = go.transform.localScale.x;
                        property.FindPropertyRelative("manualStartFloat").floatValue = s;
                        break;
                    }
                case AnimationProperty.AnimationType.Fade:
                    {
                        var g = go.GetComponent<UnityEngine.UI.MaskableGraphic>();
                        if (g == null) return;
                        property.FindPropertyRelative("manualStartFloat").floatValue = g.color.a;
                        break;
                    }
                case AnimationProperty.AnimationType.OutsideScreen_Left:
                case AnimationProperty.AnimationType.OutsideScreen_Right:
                case AnimationProperty.AnimationType.OutsideScreen_Up:
                case AnimationProperty.AnimationType.OutsideScreen_Down:
                    {
                        var rt = go.GetComponent<RectTransform>();
                        if (rt == null) return;
                        property.FindPropertyRelative("manualStartPos").vector2Value = rt.anchoredPosition;
                        break;
                    }
            }
        }

    }
}
