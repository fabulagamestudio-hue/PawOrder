using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Iung.Tools.Shared.Editor.Creation
{
    // AI GUIDANCE:
    // - When to use: when a serialized object reference should rename its referenced scene object from the variable name.
    // - Prefer: this property context workflow for keeping hierarchy names aligned with serialized references.
    // - Avoid: renaming project assets or using this tool on empty or non-reference properties.
    // - Reason: the tool preserves existing hierarchy prefixes while replacing only the semantic object name.
    [InitializeOnLoad]
    internal static class PropertyReferenceRenameTool
    {
        #region Fields

        private const string RenameMenuLabel = "Iung/Rename Referenced Object From Variable Name";
        private const string RenameUndoOperationName = "Rename Referenced Object From Variable Name";
        private const string ExistingPrefixPattern = @"^(\[[^\]]+\]\s-\s)";
        private const string DefaultObjectName = "Reference Object";

        private static readonly Regex ExistingPrefixRegex = new Regex(ExistingPrefixPattern, RegexOptions.Compiled);
        private static readonly Regex VariableNameWordRegex = new Regex(@"[A-Z]+(?=[A-Z][a-z]|[0-9]|$)|[A-Z]?[a-z]+|[0-9]+", RegexOptions.Compiled);

        #endregion

        #region Properties
        // No editor properties required.
        #endregion

        #region Unity Messages

        static PropertyReferenceRenameTool()
        {
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
        }

        #endregion

        #region Public API
        // Context registration only.
        #endregion

        #region Internal Logic

        private static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
        {
            if (!TryBuildRenameContext(property, out RenameContext renameContext))
            {
                return;
            }

            menu.AddItem(new GUIContent(RenameMenuLabel), false, () => RenameReferencedObject(renameContext));
        }

        private static bool TryBuildRenameContext(SerializedProperty property, out RenameContext renameContext)
        {
            renameContext = default;
            if (property == null)
            {
                return false;
            }

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return false;
            }

            Object referencedObject = property.objectReferenceValue;
            if (referencedObject == null)
            {
                return false;
            }

            if (!TryGetValidTargetGameObject(referencedObject, out GameObject targetGameObject))
            {
                return false;
            }

            string formattedVariableName = BuildReadableVariableName(property.name);
            if (string.IsNullOrWhiteSpace(formattedVariableName))
            {
                return false;
            }

            renameContext = new RenameContext(targetGameObject, formattedVariableName);
            return true;
        }

        private static bool TryGetValidTargetGameObject(Object referencedObject, out GameObject targetGameObject)
        {
            targetGameObject = null;
            if (referencedObject == null)
            {
                return false;
            }

            if (referencedObject is GameObject referencedGameObject)
            {
                targetGameObject = referencedGameObject;
            }
            else if (referencedObject is Component referencedComponent)
            {
                targetGameObject = referencedComponent.gameObject;
            }

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

        private static void RenameReferencedObject(RenameContext renameContext)
        {
            if (renameContext.TargetGameObject == null)
            {
                return;
            }

            string renamedObjectName = BuildRenamedObjectName(renameContext.TargetGameObject.name, renameContext.FormattedVariableName);
            if (renameContext.TargetGameObject.name == renamedObjectName)
            {
                return;
            }

            Undo.RecordObject(renameContext.TargetGameObject, RenameUndoOperationName);
            renameContext.TargetGameObject.name = renamedObjectName;
            EditorUtility.SetDirty(renameContext.TargetGameObject);
            EditorSceneManager.MarkSceneDirty(renameContext.TargetGameObject.scene);
            Selection.activeGameObject = renameContext.TargetGameObject;
            EditorGUIUtility.PingObject(renameContext.TargetGameObject);
        }

        private static string BuildRenamedObjectName(string currentObjectName, string formattedVariableName)
        {
            string preservedPrefix = ExtractExistingPrefix(currentObjectName);
            if (string.IsNullOrWhiteSpace(preservedPrefix))
            {
                return formattedVariableName;
            }

            return string.Concat(preservedPrefix, formattedVariableName);
        }

        private static string ExtractExistingPrefix(string currentObjectName)
        {
            if (string.IsNullOrWhiteSpace(currentObjectName))
            {
                return string.Empty;
            }

            Match existingPrefixMatch = ExistingPrefixRegex.Match(currentObjectName);
            if (!existingPrefixMatch.Success)
            {
                return string.Empty;
            }

            return existingPrefixMatch.Value;
        }

        private static string BuildReadableVariableName(string variableName)
        {
            string sanitizedVariableName = SanitizeVariableName(variableName);
            if (string.IsNullOrWhiteSpace(sanitizedVariableName))
            {
                return DefaultObjectName;
            }

            MatchCollection matches = VariableNameWordRegex.Matches(sanitizedVariableName);
            if (matches.Count == 0)
            {
                return CapitalizeFirstCharacter(sanitizedVariableName);
            }

            StringBuilder readableNameBuilder = new StringBuilder();
            for (int index = 0; index < matches.Count; index++)
            {
                string currentWord = matches[index].Value;
                if (string.IsNullOrWhiteSpace(currentWord))
                {
                    continue;
                }

                if (readableNameBuilder.Length > 0)
                {
                    readableNameBuilder.Append(' ');
                }

                readableNameBuilder.Append(CapitalizeWord(currentWord));
            }

            return readableNameBuilder.Length > 0 ? readableNameBuilder.ToString() : DefaultObjectName;
        }

        private static string SanitizeVariableName(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return string.Empty;
            }

            string sanitizedValue = variableName.Trim();

            while (sanitizedValue.StartsWith("_"))
            {
                sanitizedValue = sanitizedValue.Substring(1);
            }

            if (sanitizedValue.StartsWith("m_") && sanitizedValue.Length > 2)
            {
                sanitizedValue = sanitizedValue.Substring(2);
            }

            return sanitizedValue.Trim();
        }

        private static string CapitalizeWord(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.Length == 1)
            {
                return char.ToUpperInvariant(value[0]).ToString();
            }

            if (IsAllUppercase(value) || IsAllDigits(value))
            {
                return value;
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
        }

        private static string CapitalizeFirstCharacter(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.Length == 1)
            {
                return char.ToUpperInvariant(value[0]).ToString();
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static bool IsAllUppercase(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char currentCharacter = value[index];
                if (char.IsLetter(currentCharacter) && !char.IsUpper(currentCharacter))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAllDigits(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (!char.IsDigit(value[index]))
                {
                    return false;
                }
            }

            return value.Length > 0;
        }

        private readonly struct RenameContext
        {
            #region Fields

            public readonly GameObject TargetGameObject;
            public readonly string FormattedVariableName;

            #endregion

            #region Properties
            // No additional properties required.
            #endregion

            #region Unity Messages
            // Not applicable for this editor struct.
            #endregion

            #region Public API

            public RenameContext(GameObject targetGameObject, string formattedVariableName)
            {
                TargetGameObject = targetGameObject;
                FormattedVariableName = formattedVariableName;
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
        // All editor bindings are declared above through the property contextual menu hook.
        #endregion
    }

    /*
     * Technical note:
     * - The tool is registered through EditorApplication.contextualPropertyMenu so the action
     *   appears only on serialized properties in the Inspector.
     * - Only scene GameObjects referenced directly or through Components are supported, which
     *   prevents accidental asset renames when the workflow is intended for hierarchy objects.
     * - Variable names are normalized into readable words for hierarchy clarity, while existing
     *   prefixes that already follow the "[Prefix] - Name" convention are preserved.
     * - The rename action records Undo and marks the scene dirty to keep the editor workflow safe.
     */
}
