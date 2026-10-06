using UnityEditor;
using UnityEngine;

namespace Iung.Animation
{
    public sealed class AnimationSettingsWindow : EditorWindow
    {
        private AnimationGlobalSettings settings;
        private SerializedObject serializedSettings;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Iung/Animation/Settings")]
        public static void OpenWindow()
        {
            AnimationSettingsWindow window = GetWindow<AnimationSettingsWindow>("Animation Settings");
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            ReloadSettings();
        }

        private void OnFocus()
        {
            if (settings == null)
            {
                ReloadSettings();
            }
        }

        private void ReloadSettings()
        {
            settings = AnimationSettingsService.GetOrCreateSettings();
            serializedSettings = settings != null ? new SerializedObject(settings) : null;
        }

        private void OnGUI()
        {
            if (settings == null || serializedSettings == null)
            {
                EditorGUILayout.HelpBox("Could not load AnimationGlobalSettings.", MessageType.Error);
                if (GUILayout.Button("Retry"))
                {
                    ReloadSettings();
                }
                return;
            }

            serializedSettings.Update();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawDefaultSection();
            EditorGUILayout.Space(8f);
            DrawSelectionActions();

            EditorGUILayout.EndScrollView();

            if (serializedSettings.ApplyModifiedProperties())
            {
                AnimationSettingsService.SaveSettings(settings);
            }
        }

        private void DrawDefaultSection()
        {
            EditorGUILayout.LabelField("Global Defaults", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultAnimationTime"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultShowDelay"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultHideDelay"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultEaseIn"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultEaseOut"));

            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultRectTweenDuration"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultRectTweenEase"));

            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("previewMovementByDefault"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("logLevel"));

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to Plugin Defaults"))
                {
                    Undo.RecordObject(settings, "Reset Animation Global Settings");
                    settings.ResetToPluginDefaults();
                    EditorUtility.SetDirty(settings);
                    serializedSettings.Update();
                    AnimationSettingsService.SaveSettings(settings);
                }

                if (GUILayout.Button("Ping Settings Asset"))
                {
                    AnimationSettingsService.SelectSettingsAsset();
                }
            }
        }

        private void DrawSelectionActions()
        {
            EditorGUILayout.LabelField("Selection Actions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These actions are editor-only helpers.\n" +
                "They do not change runtime behavior automatically.",
                MessageType.Info);

            if (GUILayout.Button("Apply Global Defaults to Selected AnimationProperty"))
            {
                int changed = AnimationSettingsApplyService.ApplyDefaultsToSelectedAnimationProperties(settings);
                Debug.Log($"[AnimationSettings] Applied defaults to {changed} AnimationProperty item(s).");
            }

            if (GUILayout.Button("Apply RectTween Defaults to Selected UI_RectTransformTweener"))
            {
                int changed = AnimationSettingsApplyService.ApplyDefaultsToSelectedRectTweeners(settings);
                Debug.Log($"[AnimationSettings] Applied RectTween defaults to {changed} UI_RectTransformTweener component(s).");
            }
        }
    }
}
