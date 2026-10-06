using UnityEngine;
using UnityEditor;

namespace Iung.Animation
{
    [CustomEditor(typeof(UI_RectTransformTweener))]
    public class RectTransformTweenerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UI_RectTransformTweener tweener = (UI_RectTransformTweener)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Apply Global Defaults"))
            {
                AnimationGlobalSettings settings = AnimationSettingsService.GetOrCreateSettings();
                if (settings != null)
                {
                    Undo.RecordObject(tweener, "Apply Animation Global Defaults");
                    tweener.transitionDuration = settings.DefaultRectTweenDuration;
                    tweener.easeType = settings.DefaultRectTweenEase;
                    EditorUtility.SetDirty(tweener);
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Current as State A"))
            {
                if (tweener.targetRectTransform == null)
                    tweener.targetRectTransform = tweener.GetComponent<RectTransform>();

                SetState(ref tweener.stateA, tweener.targetRectTransform);
            }

            if (GUILayout.Button("Force to State A"))
            {


                tweener.ForceState(tweener.stateA);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Current as State B"))
            {
                if (tweener.targetRectTransform == null)
                    tweener.targetRectTransform = tweener.GetComponent<RectTransform>();

                SetState(ref tweener.stateB, tweener.targetRectTransform);
            }
            if (GUILayout.Button("Force to State B"))
            {
                tweener.ForceState(tweener.stateB);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Toggle State"))
            {
                tweener.ToggleState();
            }
        }

        private void SetState(ref RectTransformState state, RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                Debug.LogError("Target RectTransform is not assigned.");
                return;
            }

            state.anchoredPosition = rectTransform.anchoredPosition;
            state.sizeDelta = rectTransform.sizeDelta;
            state.scale = rectTransform.localScale;
            state.rotation = rectTransform.rotation;

            EditorUtility.SetDirty(target);
        }
    }
}
