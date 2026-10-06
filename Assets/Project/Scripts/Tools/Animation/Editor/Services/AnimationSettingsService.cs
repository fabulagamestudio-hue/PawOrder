using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Iung.Animation
{
    [InitializeOnLoad]
    public static class AnimationSettingsService
    {
        private static bool hasLoggedMultipleAssetsWarning;

        static AnimationSettingsService()
        {
            EditorApplication.delayCall += EnsureSettingsAssetExists;
        }

        public static AnimationGlobalSettings GetOrCreateSettings()
        {
            string settingsAssetPath = ResolveSettingsAssetPath();
            if (!string.IsNullOrWhiteSpace(settingsAssetPath))
            {
                AnimationGlobalSettings settings = AssetDatabase.LoadAssetAtPath<AnimationGlobalSettings>(settingsAssetPath);
                if (settings != null)
                {
                    return settings;
                }
            }

            EnsureSettingsAssetExists();
            settingsAssetPath = ResolveSettingsAssetPath();
            return string.IsNullOrWhiteSpace(settingsAssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<AnimationGlobalSettings>(settingsAssetPath);
        }

        public static void EnsureSettingsAssetExists()
        {
            string settingsAssetPath = ResolveSettingsAssetPath();
            if (string.IsNullOrWhiteSpace(settingsAssetPath))
            {
                return;
            }

            AnimationGlobalSettings existingSettings = AssetDatabase.LoadAssetAtPath<AnimationGlobalSettings>(settingsAssetPath);
            if (existingSettings != null)
            {
                return;
            }

            EnsureFolderPathExists(Path.GetDirectoryName(settingsAssetPath));

            AnimationGlobalSettings settings = ScriptableObject.CreateInstance<AnimationGlobalSettings>();
            try
            {
                AssetDatabase.CreateAsset(settings, settingsAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[AnimationSettings] Failed to create settings asset at '{settingsAssetPath}'. {exception.Message}");
                Object.DestroyImmediate(settings);
            }
        }

        public static void SaveSettings(AnimationGlobalSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        public static void SelectSettingsAsset()
        {
            AnimationGlobalSettings settings = GetOrCreateSettings();
            if (settings == null)
            {
                return;
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        internal static string GetDllDirectoryPath()
        {
            string[] precompiledAssemblyPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All);
            string assemblyFileName = AnimationPaths.AssemblyName + ".dll";

            for (int i = 0; i < precompiledAssemblyPaths.Length; i++)
            {
                string candidateAssetPath = TryConvertToAssetPath(precompiledAssemblyPaths[i]);
                if (string.IsNullOrWhiteSpace(candidateAssetPath))
                {
                    continue;
                }

                if (!string.Equals(Path.GetFileName(candidateAssetPath), assemblyFileName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return AnimationPathUtility.NormalizeAssetPath(Path.GetDirectoryName(candidateAssetPath));
            }

            return string.Empty;
        }

        internal static string GetSettingsAssetPathFromDll()
        {
            string dllDirectory = GetDllDirectoryPath();
            if (string.IsNullOrWhiteSpace(dllDirectory))
            {
                return string.Empty;
            }

            string rawSettingsPath = Path.Combine(dllDirectory, AnimationPaths.SettingsRelativeFolder, AnimationPaths.SettingsAssetFileName);
            return AnimationPathUtility.NormalizeAssetPath(rawSettingsPath);
        }

        private static string ResolveSettingsAssetPath()
        {
            string dllBasedPath = GetSettingsAssetPathFromDll();
            if (!string.IsNullOrWhiteSpace(dllBasedPath))
            {
                return dllBasedPath;
            }

            string[] guids = AssetDatabase.FindAssets("t:AnimationGlobalSettings");
            if (guids.Length > 1 && !hasLoggedMultipleAssetsWarning)
            {
                hasLoggedMultipleAssetsWarning = true;
                Debug.LogWarning($"[AnimationSettings] Multiple AnimationGlobalSettings assets were found ({guids.Length}). Using the first valid asset.");
            }

            string firstValidAssetPath = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => AssetDatabase.LoadAssetAtPath<AnimationGlobalSettings>(path) != null);

            if (!string.IsNullOrWhiteSpace(firstValidAssetPath))
            {
                return AnimationPathUtility.NormalizeAssetPath(firstValidAssetPath);
            }

            return AnimationPathUtility.NormalizeAssetPath(
                Path.Combine(AnimationPaths.FallbackRootFolder, AnimationPaths.SettingsRelativeFolder, AnimationPaths.SettingsAssetFileName));
        }

        private static string TryConvertToAssetPath(string rawPath)
        {
            string normalizedPath = AnimationPathUtility.NormalizeAssetPath(rawPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return string.Empty;
            }

            if (AnimationPathUtility.IsAssetsPath(normalizedPath))
            {
                return normalizedPath;
            }

            string normalizedDataPath = AnimationPathUtility.NormalizeAssetPath(Application.dataPath);
            if (!normalizedPath.StartsWith(normalizedDataPath + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string suffix = normalizedPath.Substring(normalizedDataPath.Length).TrimStart('/');
            return AnimationPathUtility.NormalizeAssetPath("Assets/" + suffix);
        }

        private static void EnsureFolderPathExists(string folderPath)
        {
            string normalizedFolderPath = AnimationPathUtility.NormalizeAssetPath(folderPath);
            if (string.IsNullOrWhiteSpace(normalizedFolderPath))
            {
                return;
            }

            string[] segments = normalizedFolderPath.Split('/');
            string currentPath = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string nextPath = currentPath + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[i]);
                }

                currentPath = nextPath;
            }
        }
    }
}
