using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Iung.Tools.Shared.Editor.Creation
{
    // AI GUIDANCE:
    // - When to use: when standardizing scene object naming from inspector component context menus.
    // - Prefer: this naming workflow to keep hierarchy names consistent with component-driven prefixes.
    // - Avoid: adding extra rename menu variations that conflict with the established "[Prefix] - Name" pattern.
    // - Reason: existing prefixes are replaced (not stacked) and technical prefixes like UI or TMP are normalized.
    internal static class InspectorComponentRenameTool
    {
        #region Fields

        private const string RenameMenuPath = "CONTEXT/Component/Iung/Prefix GameObject Name";
        private const string RenameUndoOperationName = "Prefix GameObject Name With Component";
        private const string ExistingPrefixPattern = @"^\[[^\]]+\]\s-\s";
        private const string EmptyPrefix = "Empty";
        private const string DefaultPrefix = "Component";

        private static readonly Regex ExistingPrefixRegex = new Regex(ExistingPrefixPattern, RegexOptions.Compiled);
        private static readonly Regex PascalCaseWordRegex = new Regex(@"[A-Z][a-z0-9]*|[A-Z]+(?![a-z])|[a-z0-9]+", RegexOptions.Compiled);
        private static readonly HashSet<string> TechnicalPrefixes = new HashSet<string>
        {
            "UI",
            "TMP"
        };

        #endregion

        #region Properties
        // No editor properties required.
        #endregion

        #region Unity Messages
        // Not applicable for this editor utility.
        #endregion

        #region Public API

        [MenuItem(RenameMenuPath, false, 2000)]
        private static void PrefixGameObjectName(MenuCommand menuCommand)
        {
            Component targetComponent = menuCommand.context as Component;
            if (!TryGetValidTargetGameObject(targetComponent, out GameObject targetGameObject))
            {
                Debug.LogWarning("[InspectorComponentRenameTool] The clicked component does not belong to a valid scene GameObject.");
                return;
            }

            string componentClassName = targetComponent.GetType().Name;
            string renamedObjectName = BuildRenamedObjectName(componentClassName, targetGameObject.name);
            if (targetGameObject.name == renamedObjectName)
            {
                return;
            }

            Undo.RecordObject(targetGameObject, RenameUndoOperationName);
            targetGameObject.name = renamedObjectName;
            EditorUtility.SetDirty(targetGameObject);
            EditorSceneManager.MarkSceneDirty(targetGameObject.scene);
            Selection.activeGameObject = targetGameObject;
            EditorGUIUtility.PingObject(targetGameObject);
        }

        [MenuItem(RenameMenuPath, true)]
        private static bool ValidatePrefixGameObjectName(MenuCommand menuCommand)
        {
            Component targetComponent = menuCommand.context as Component;
            return TryGetValidTargetGameObject(targetComponent, out _);
        }

        #endregion

        #region Internal Logic

        private static bool TryGetValidTargetGameObject(Component targetComponent, out GameObject targetGameObject)
        {
            targetGameObject = null;
            if (targetComponent == null)
            {
                return false;
            }

            targetGameObject = targetComponent.gameObject;
            if (targetGameObject == null)
            {
                return false;
            }

            if (EditorUtility.IsPersistent(targetGameObject))
            {
                targetGameObject = null;
                return false;
            }

            if (!targetGameObject.scene.IsValid() || !targetGameObject.scene.isLoaded)
            {
                targetGameObject = null;
                return false;
            }

            return true;
        }

        private static string BuildRenamedObjectName(string componentClassName, string currentObjectName)
        {
            string sanitizedObjectName = RemoveExistingPrefix(currentObjectName);
            string componentPrefix = BuildComponentPrefix(componentClassName);
            return string.Concat("[", componentPrefix, "] - ", sanitizedObjectName);
        }

        private static string BuildComponentPrefix(string componentClassName)
        {
            if (string.IsNullOrWhiteSpace(componentClassName))
            {
                return DefaultPrefix;
            }

            if (componentClassName == nameof(Transform) || componentClassName == nameof(RectTransform))
            {
                return EmptyPrefix;
            }

            string normalizedClassName = componentClassName.Trim();
            string semanticName = RemoveKnownTechnicalPrefixes(normalizedClassName);
            string combinedWords = ExtractFirstTwoWordsFromPascalCase(semanticName);

            if (string.IsNullOrWhiteSpace(combinedWords))
            {
                return DefaultPrefix;
            }

            return combinedWords;
        }

        private static string RemoveKnownTechnicalPrefixes(string className)
        {
            string[] nameParts = className.Split('_');
            for (int index = 0; index < nameParts.Length; index++)
            {
                string currentPart = nameParts[index];
                if (string.IsNullOrWhiteSpace(currentPart))
                {
                    continue;
                }

                if (IsTechnicalPrefix(currentPart))
                {
                    continue;
                }

                return currentPart;
            }

            return className;
        }

        private static bool IsTechnicalPrefix(string namePart)
        {
            return TechnicalPrefixes.Contains(namePart);
        }

        private static string ExtractFirstTwoWordsFromPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            MatchCollection matches = PascalCaseWordRegex.Matches(value);
            if (matches.Count == 0)
            {
                return value;
            }

            StringBuilder prefixBuilder = new StringBuilder();
            int wordsToUse = Mathf.Min(2, matches.Count);

            for (int index = 0; index < wordsToUse; index++)
            {
                prefixBuilder.Append(matches[index].Value);
            }

            return prefixBuilder.ToString();
        }

        private static string RemoveExistingPrefix(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return "GameObject";
            }

            return ExistingPrefixRegex.Replace(objectName, string.Empty);
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
     * - The menu is registered against Component so the same action can be reused
     *   from generic Inspector component context menus.
     * - The rename logic always replaces a previous "[Something] - " prefix to keep
     *   names clean and predictable.
     * - Transform and RectTransform are normalized to the "Empty" prefix.
     * - Known technical prefixes such as "UI" and "TMP" are ignored so the final
     *   prefix is more semantic in the Hierarchy.
     * - The component prefix uses up to the first two PascalCase words without spaces
     *   to preserve readability while avoiding ambiguous short names.
     * - Persistent objects such as prefab assets are ignored to avoid renaming
     *   project assets when the intent is a scene-object workflow.
     */
}
