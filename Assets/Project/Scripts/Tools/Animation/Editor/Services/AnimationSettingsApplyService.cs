using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Iung.Animation
{
    public static class AnimationSettingsApplyService
    {
        public static bool ApplyDefaultsToAnimationProperty(SerializedProperty animationProperty, AnimationGlobalSettings settings)
        {
            if (animationProperty == null || settings == null)
            {
                return false;
            }

            SerializedProperty timeProp = animationProperty.FindPropertyRelative("time");
            SerializedProperty delayProp = animationProperty.FindPropertyRelative("delay");
            SerializedProperty delayHideProp = animationProperty.FindPropertyRelative("delayHide");
            SerializedProperty easeInProp = animationProperty.FindPropertyRelative("_easeIn");
            SerializedProperty easeOutProp = animationProperty.FindPropertyRelative("_easeOut");

            if (timeProp == null || delayProp == null || delayHideProp == null || easeInProp == null || easeOutProp == null)
            {
                return false;
            }

            timeProp.floatValue = settings.DefaultAnimationTime;
            delayProp.floatValue = settings.DefaultShowDelay;
            delayHideProp.floatValue = settings.DefaultHideDelay;
            easeInProp.enumValueIndex = (int)settings.DefaultEaseIn;
            easeOutProp.enumValueIndex = (int)settings.DefaultEaseOut;
            return true;
        }

        public static int ApplyDefaultsToSelectedAnimationProperties(AnimationGlobalSettings settings)
        {
            if (settings == null)
            {
                return 0;
            }

            int changedCount = 0;
            GameObject[] selectedGameObjects = Selection.gameObjects;
            for (int i = 0; i < selectedGameObjects.Length; i++)
            {
                GameObject gameObject = selectedGameObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                Component[] components = gameObject.GetComponents<Component>();
                for (int c = 0; c < components.Length; c++)
                {
                    Component component = components[c];
                    if (component == null)
                    {
                        continue;
                    }

                    SerializedObject serializedObject = new SerializedObject(component);
                    SerializedProperty iterator = serializedObject.GetIterator();
                    bool hasChanges = false;
                    bool enterChildren = true;

                    while (iterator.NextVisible(enterChildren))
                    {
                        enterChildren = true;
                        if (iterator.propertyType != SerializedPropertyType.Generic)
                        {
                            continue;
                        }

                        if (ApplyDefaultsToAnimationProperty(iterator, settings))
                        {
                            hasChanges = true;
                            changedCount++;
                        }
                    }

                    if (hasChanges)
                    {
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(component);
                    }
                }
            }

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return changedCount;
        }

        public static int ApplyDefaultsToSelectedRectTweeners(AnimationGlobalSettings settings)
        {
            if (settings == null)
            {
                return 0;
            }

            HashSet<UI_RectTransformTweener> targets = new HashSet<UI_RectTransformTweener>();
            GameObject[] selectedGameObjects = Selection.gameObjects;
            for (int i = 0; i < selectedGameObjects.Length; i++)
            {
                GameObject selectedGameObject = selectedGameObjects[i];
                if (selectedGameObject == null)
                {
                    continue;
                }

                UI_RectTransformTweener[] tweeners = selectedGameObject.GetComponentsInChildren<UI_RectTransformTweener>(true);
                for (int t = 0; t < tweeners.Length; t++)
                {
                    if (tweeners[t] != null)
                    {
                        targets.Add(tweeners[t]);
                    }
                }
            }

            int changedCount = 0;
            foreach (UI_RectTransformTweener tweener in targets)
            {
                Undo.RecordObject(tweener, "Apply Animation Global Defaults");
                tweener.transitionDuration = settings.DefaultRectTweenDuration;
                tweener.easeType = settings.DefaultRectTweenEase;
                EditorUtility.SetDirty(tweener);
                changedCount++;
            }

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return changedCount;
        }
    }
}
