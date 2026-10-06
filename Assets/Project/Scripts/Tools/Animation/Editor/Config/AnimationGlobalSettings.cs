using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Iung.Animation
{
    public enum AnimationSettingsLogLevel
    {
        None,
        Warnings,
        Verbose
    }

    [CreateAssetMenu(fileName = "AnimationGlobalSettings", menuName = "Iung/Animation/Settings")]
    public sealed class AnimationGlobalSettings : ScriptableObject
    {
        [Header("AnimationProperty Defaults")]
        [SerializeField, Min(0f)]
        private float defaultAnimationTime = 0.3f;

        [SerializeField, Min(0f)]
        private float defaultShowDelay = 0f;

        [SerializeField, Min(0f)]
        private float defaultHideDelay = 0f;

        [SerializeField]
        private Ease defaultEaseIn = Ease.OutQuint;

        [SerializeField]
        private Ease defaultEaseOut = Ease.InQuint;

        [Header("RectTween Defaults")]
        [SerializeField, Min(0f)]
        private float defaultRectTweenDuration = 0.5f;

        [SerializeField]
        private Ease defaultRectTweenEase = Ease.InOutBack;

        [Header("Editor")]
        [SerializeField]
        private bool previewMovementByDefault;

        [SerializeField]
        private AnimationSettingsLogLevel logLevel = AnimationSettingsLogLevel.Warnings;

        public float DefaultAnimationTime => defaultAnimationTime;
        public float DefaultShowDelay => defaultShowDelay;
        public float DefaultHideDelay => defaultHideDelay;
        public Ease DefaultEaseIn => defaultEaseIn;
        public Ease DefaultEaseOut => defaultEaseOut;
        public float DefaultRectTweenDuration => defaultRectTweenDuration;
        public Ease DefaultRectTweenEase => defaultRectTweenEase;
        public bool PreviewMovementByDefault => previewMovementByDefault;
        public AnimationSettingsLogLevel LogLevel => logLevel;

        public void ResetToPluginDefaults()
        {
            defaultAnimationTime = 0.3f;
            defaultShowDelay = 0f;
            defaultHideDelay = 0f;
            defaultEaseIn = Ease.OutQuint;
            defaultEaseOut = Ease.InQuint;
            defaultRectTweenDuration = 0.5f;
            defaultRectTweenEase = Ease.InOutBack;
            previewMovementByDefault = false;
            logLevel = AnimationSettingsLogLevel.Warnings;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(AnimationGlobalSettings))]
    internal sealed class AnimationGlobalSettingsEditor : UnityEditor.Editor
    {
        private const string WindowThumbPath = "Assets/Project/Tools/Animation/Editor/UI/Thumb.png";
        private static readonly Color WindowThumbBackgroundColor = new Color32(0x20, 0x0C, 0x3B, 0xFF);

        private Sprite windowThumbSprite;
        private Texture2D windowThumbTexture;
        private Rect windowThumbSourceRect;

        private void OnEnable()
        {
            LoadWindowThumb();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawWindowThumb();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWindowThumb()
        {
            if (windowThumbTexture == null)
            {
                return;
            }

            const float bannerHeight = 250f;
            Rect layoutRect = GUILayoutUtility.GetRect(0f, bannerHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(layoutRect, WindowThumbBackgroundColor);

            float sourceWidth = windowThumbSourceRect.width > 0f ? windowThumbSourceRect.width : windowThumbTexture.width;
            float sourceHeight = windowThumbSourceRect.height > 0f ? windowThumbSourceRect.height : windowThumbTexture.height;
            if (sourceWidth <= 0f || sourceHeight <= 0f)
            {
                return;
            }

            float maxImageWidth = Mathf.Max(1f, layoutRect.width - 12f);
            float maxImageHeight = Mathf.Max(1f, layoutRect.height - 12f);
            float imageScale = Mathf.Min(maxImageWidth / sourceWidth, maxImageHeight / sourceHeight);
            float imageWidth = sourceWidth * imageScale;
            float imageHeight = sourceHeight * imageScale;

            Rect imageRect = new Rect(
                layoutRect.x + (layoutRect.width - imageWidth) * 0.5f,
                layoutRect.y + (layoutRect.height - imageHeight) * 0.5f,
                imageWidth,
                imageHeight);

            if (windowThumbSprite != null)
            {
                Rect uv = new Rect(
                    windowThumbSourceRect.x / windowThumbTexture.width,
                    windowThumbSourceRect.y / windowThumbTexture.height,
                    windowThumbSourceRect.width / windowThumbTexture.width,
                    windowThumbSourceRect.height / windowThumbTexture.height);
                GUI.DrawTextureWithTexCoords(imageRect, windowThumbTexture, uv, true);
            }
            else
            {
                GUI.DrawTexture(imageRect, windowThumbTexture, ScaleMode.StretchToFill, true);
            }

            EditorGUILayout.Space(4f);
        }

        private void LoadWindowThumb()
        {
            windowThumbSprite = null;
            windowThumbTexture = null;
            windowThumbSourceRect = Rect.zero;

            Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(WindowThumbPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite sprite)
                {
                    windowThumbSprite = sprite;
                    windowThumbTexture = sprite.texture;
                    windowThumbSourceRect = ResolveSpriteSourceRect(sprite);
                    return;
                }
            }

            windowThumbTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WindowThumbPath);
            if (windowThumbTexture != null && windowThumbTexture.width > 0)
            {
                windowThumbSourceRect = new Rect(0f, 0f, windowThumbTexture.width, windowThumbTexture.height);
            }
        }

        private static Rect ResolveSpriteSourceRect(Sprite sprite)
        {
            try
            {
                Rect textureRect = sprite.textureRect;
                if (textureRect.width > 0f && textureRect.height > 0f)
                {
                    return textureRect;
                }
            }
            catch
            {
                // Ignore and fallback to rect below.
            }

            return sprite.rect;
        }
    }
#endif
}
