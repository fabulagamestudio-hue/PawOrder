using System;
using System.Collections.Generic;
using System.Linq;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    public sealed class QuestionPromptMenuOrganizerWindow : EditorWindow
    {
        #region Constants

        private const string DefaultAssetSearchFolder = "Assets";
        private const float LeftPanelMinWidth = 300f;
        private const float PreviewPanelMinWidth = 420f;
        private const float InspectorPanelMinWidth = 500f;
        private const float LayoutSpacing = 6f;
        private const float LeftPanelRatio = 0.25f;
        private const float PreviewPanelRatio = 0.35f;
        private const float InspectorPanelRatio = 0.40f;
        private const float WindowPadding = 12f;
        private const float InspectorLabelWidth = 150f;

        #endregion

        #region Fields

        [SerializeField]
        [Tooltip("Folder used by AssetDatabase.FindAssets to scan QuestionPromptData assets.")]
        private string assetSearchFolder = DefaultAssetSearchFolder;

        [SerializeField]
        [Tooltip("Optional runtime case data used to preview all prompts from a specific case.")]
        private CaseData previewCaseData;

        [SerializeField]
        [Tooltip("Optional NPC used to preview self-pronoun replacement and interaction availability.")]
        private CharacterData previewNpc;

        [SerializeField]
        [Tooltip("Shows hidden prompts in the preview tree for audit purposes.")]
        private bool includeHiddenPrompts;

        [SerializeField]
        [Tooltip("Shows only prompts that still need menu configuration.")]
        private bool showOnlyUnconfigured;

        [SerializeField]
        [Tooltip("Search text used to filter prompts by id, display name, menu label or target id.")]
        private string searchText;

        [SerializeField]
        [Tooltip("Selected menu group filter.")]
        private QuestionPromptMenuGroup groupFilter = QuestionPromptMenuGroup.Character;

        [SerializeField]
        [Tooltip("Enables the menu group filter.")]
        private bool useGroupFilter;

        private readonly List<QuestionPromptData> prompts = new List<QuestionPromptData>();
        private readonly List<CharacterInteractionData> interactions = new List<CharacterInteractionData>();
        private readonly List<QuestionPromptData> filteredPrompts = new List<QuestionPromptData>();
        private readonly List<string> validationMessages = new List<string>();

        private QuestionPromptData selectedPrompt;
        private SerializedObject selectedPromptObject;
        private Vector2 mainLayoutScroll;
        private Vector2 promptListScroll;
        private Vector2 previewScroll;
        private Vector2 inspectorScroll;
        private Vector2 validationScroll;

        #endregion

        #region Unity Messages

        [MenuItem("Fabula/Paw Order/Question Prompt Menu Organizer")]
        public static void OpenWindow()
        {
            QuestionPromptMenuOrganizerWindow window = GetWindow<QuestionPromptMenuOrganizerWindow>();
            window.titleContent = new GUIContent("Prompt Menu Organizer");
            window.minSize = new Vector2(760f, 420f);
            window.ReloadAssets();
        }

        private void OnEnable()
        {
            ReloadAssets();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawMainLayout();
        }

        #endregion

        #region Internal Logic

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.toolbar))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    assetSearchFolder = EditorGUILayout.TextField(assetSearchFolder, GUILayout.MinWidth(220f));

                    if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    {
                        ReloadAssets();
                    }

                    if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    {
                        ValidatePrompts();
                    }

                    if (GUILayout.Button("Save Assets", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                    {
                        AssetDatabase.SaveAssets();
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField("Prompts", prompts.Count.ToString(), GUILayout.Width(88f));
                    EditorGUILayout.LabelField("Visible", filteredPrompts.Count.ToString(), GUILayout.Width(88f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    searchText = EditorGUILayout.TextField("Search", searchText);
                    showOnlyUnconfigured = GUILayout.Toggle(showOnlyUnconfigured, "Unconfigured", EditorStyles.toolbarButton, GUILayout.Width(104f));
                    useGroupFilter = GUILayout.Toggle(useGroupFilter, "Group", EditorStyles.toolbarButton, GUILayout.Width(64f));

                    using (new EditorGUI.DisabledScope(!useGroupFilter))
                    {
                        groupFilter = (QuestionPromptMenuGroup)EditorGUILayout.EnumPopup(groupFilter, GUILayout.Width(120f));
                    }
                }
            }
        }

        private void DrawMainLayout()
        {
            RefreshFilteredPrompts();

            float minimumContentWidth = LeftPanelMinWidth + PreviewPanelMinWidth + InspectorPanelMinWidth + (LayoutSpacing * 2f);
            float availableWidth = Mathf.Max(0f, position.width - WindowPadding - GUI.skin.verticalScrollbar.fixedWidth);
            float contentWidth = Mathf.Max(availableWidth, minimumContentWidth);

            float usableContentWidth = contentWidth - (LayoutSpacing * 2f);
            float leftWidth = Mathf.Max(LeftPanelMinWidth, usableContentWidth * LeftPanelRatio);
            float previewWidth = Mathf.Max(PreviewPanelMinWidth, usableContentWidth * PreviewPanelRatio);
            float inspectorWidth = Mathf.Max(InspectorPanelMinWidth, usableContentWidth * InspectorPanelRatio);

            float calculatedContentWidth = leftWidth + previewWidth + inspectorWidth + (LayoutSpacing * 2f);

            mainLayoutScroll = EditorGUILayout.BeginScrollView(
                mainLayoutScroll,
                true,
                false,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(calculatedContentWidth), GUILayout.ExpandHeight(true)))
            {
                DrawPromptListPanel(leftWidth);
                GUILayout.Space(LayoutSpacing);
                DrawPreviewPanel(previewWidth);
                GUILayout.Space(LayoutSpacing);
                DrawInspectorPanel(inspectorWidth);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPromptListPanel(float panelWidth)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("Question Assets", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
                {
                    promptListScroll = EditorGUILayout.BeginScrollView(
                        promptListScroll,
                        true,
                        true,
                        GUILayout.ExpandHeight(true));

                    foreach (QuestionPromptData prompt in filteredPrompts)
                    {
                        DrawPromptListButton(prompt);
                    }

                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawPromptListButton(QuestionPromptData prompt)
        {
            if (prompt == null)
            {
                return;
            }

            string label = GetPromptListLabel(prompt);
            GUIStyle style = selectedPrompt == prompt ? EditorStyles.toolbarButton : EditorStyles.miniButton;

            if (GUILayout.Button(label, style))
            {
                SelectPrompt(prompt);
            }
        }

        private void DrawPreviewPanel(float panelWidth)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("Menu Preview", EditorStyles.boldLabel);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    previewCaseData = (CaseData)EditorGUILayout.ObjectField("Case Data", previewCaseData, typeof(CaseData), false);
                    previewNpc = (CharacterData)EditorGUILayout.ObjectField("Preview NPC", previewNpc, typeof(CharacterData), false);
                    includeHiddenPrompts = EditorGUILayout.Toggle("Include Hidden", includeHiddenPrompts);
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
                {
                    previewScroll = EditorGUILayout.BeginScrollView(
                        previewScroll,
                        true,
                        true,
                        GUILayout.ExpandHeight(true));
                    DrawMenuPreview();
                    EditorGUILayout.EndScrollView();
                }

                DrawValidationPanel();
            }
        }

        private void DrawInspectorPanel(float panelWidth)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("Selected Prompt", EditorStyles.boldLabel);

                if (selectedPrompt == null)
                {
                    EditorGUILayout.HelpBox("Select a QuestionPromptData asset to edit its menu metadata.", MessageType.Info);
                    return;
                }

                if (selectedPromptObject == null || selectedPromptObject.targetObject != selectedPrompt)
                {
                    selectedPromptObject = new SerializedObject(selectedPrompt);
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
                {
                    inspectorScroll = EditorGUILayout.BeginScrollView(
                        inspectorScroll,
                        true,
                        true,
                        GUILayout.ExpandHeight(true));

                    float previousLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = InspectorLabelWidth;

                    selectedPromptObject.Update();

                    DrawReadonlyPromptSummary();
                    EditorGUILayout.Space(6f);
                    DrawMenuProperties();
                    EditorGUILayout.Space(6f);
                    DrawSelectedPromptActions();

                    selectedPromptObject.ApplyModifiedProperties();
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawReadonlyPromptSummary()
        {
            DrawReadonlyWrappedField("Asset", selectedPrompt.name);
            DrawReadonlyWrappedField("Prompt Id", selectedPrompt.PromptId);
            DrawReadonlyWrappedField("Question Id", selectedPrompt.QuestionId);
            DrawReadonlyWrappedField("Display Name", selectedPrompt.DisplayName);
            DrawReadonlyWrappedField("Legacy Group", selectedPrompt.QuestionGroup);
        }


        private void DrawReadonlyWrappedField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(96f));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
            }
        }

        private void DrawMenuProperties()
        {
            EditorGUILayout.LabelField("Menu Metadata", EditorStyles.boldLabel);
            DrawProperty("menuGroup");
            DrawProperty("menuTargetType");
            DrawProperty("menuTargetId");
            DrawProperty("menuLabel");
            DrawProperty("useSelfPronounWhenTargetMatchesNpc");
            DrawProperty("sortOrder");
            DrawProperty("startsVisible");
            DrawProperty("isFollowUpQuestion");
        }

        private void DrawSelectedPromptActions()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Use Display Name As Menu Label"))
            {
                SetStringProperty("menuLabel", selectedPrompt.DisplayName);
            }

            if (GUILayout.Button("Use Character Target From Preview NPC"))
            {
                if (previewNpc == null)
                {
                    EditorUtility.DisplayDialog("Missing Preview NPC", "Assign a Preview NPC first.", "OK");
                    return;
                }

                SetEnumProperty("menuTargetType", QuestionPromptMenuTargetType.Character);
                SetStringProperty("menuTargetId", "Character." + previewNpc.CharacterId);
            }

            if (GUILayout.Button("Ping Asset"))
            {
                EditorGUIUtility.PingObject(selectedPrompt);
            }
        }

        private void DrawMenuPreview()
        {
            List<QuestionPromptData> sourcePrompts = GetPreviewPromptSource();
            CaseRuntimeState previewRuntime = BuildPreviewRuntime();
            List<CharacterInteractionData> npcInteractions = GetPreviewNpcInteractions();
            IReadOnlyList<QuestionPromptGroupMenu> groups = QuestionMenuBuilder.Build(
                sourcePrompts,
                previewRuntime,
                previewNpc,
                npcInteractions,
                includeHiddenPrompts);

            if (groups.Count == 0)
            {
                EditorGUILayout.HelpBox("No prompts match the current preview filters.", MessageType.Info);
                return;
            }

            foreach (QuestionPromptGroupMenu group in groups)
            {
                EditorGUILayout.LabelField(group.GroupLabel, EditorStyles.boldLabel);

                foreach (QuestionPromptTargetMenu target in group.Targets)
                {
                    EditorGUILayout.LabelField("  " + target.TargetLabel, EditorStyles.miniBoldLabel);

                    foreach (QuestionPromptMenuEntry entry in target.Entries)
                    {
                        DrawPreviewEntry(entry);
                    }
                }

                EditorGUILayout.Space(6f);
            }
        }

        private void DrawPreviewEntry(QuestionPromptMenuEntry entry)
        {
            string marker = entry.AvailabilityState == QuestionPromptAvailabilityState.Available
                ? "[Available]"
                : entry.AvailabilityState == QuestionPromptAvailabilityState.LockedVisible
                    ? "[Locked]"
                    : "[Hidden]";

            string newMarker = entry.IsNew ? " [New]" : string.Empty;
            EditorGUILayout.LabelField("    " + marker + " " + entry.Label + newMarker, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawValidationPanel()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(96f), GUILayout.MaxHeight(160f)))
            {
                validationScroll = EditorGUILayout.BeginScrollView(
                    validationScroll,
                    true,
                    true,
                    GUILayout.ExpandHeight(true));

                if (validationMessages.Count == 0)
                {
                    EditorGUILayout.HelpBox("Click Validate to audit menu metadata.", MessageType.Info);
                }
                else
                {
                    foreach (string message in validationMessages)
                    {
                        EditorGUILayout.HelpBox(message, MessageType.Warning);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = selectedPromptObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox("Missing serialized property: " + propertyName, MessageType.Error);
                return;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        private void ReloadAssets()
        {
            prompts.Clear();
            interactions.Clear();

            string[] searchFolders = GetSearchFolders();
            LoadAssetsOfType(prompts, searchFolders);
            LoadAssetsOfType(interactions, searchFolders);

            prompts.Sort(ComparePrompts);
            interactions.RemoveAll(interaction => interaction == null);

            if (selectedPrompt != null && !prompts.Contains(selectedPrompt))
            {
                SelectPrompt(null);
            }

            Repaint();
        }

        private void RefreshFilteredPrompts()
        {
            filteredPrompts.Clear();

            foreach (QuestionPromptData prompt in prompts)
            {
                if (!ShouldShowPrompt(prompt))
                {
                    continue;
                }

                filteredPrompts.Add(prompt);
            }
        }

        private bool ShouldShowPrompt(QuestionPromptData prompt)
        {
            if (prompt == null)
            {
                return false;
            }

            if (showOnlyUnconfigured && !IsUnconfigured(prompt))
            {
                return false;
            }

            if (useGroupFilter && prompt.MenuGroup != groupFilter)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            string normalizedSearch = searchText.Trim();
            return Contains(prompt.name, normalizedSearch)
                || Contains(prompt.PromptId, normalizedSearch)
                || Contains(prompt.QuestionId, normalizedSearch)
                || Contains(prompt.DisplayName, normalizedSearch)
                || Contains(prompt.MenuLabel, normalizedSearch)
                || Contains(prompt.MenuTargetId, normalizedSearch);
        }

        private void SelectPrompt(QuestionPromptData prompt)
        {
            selectedPrompt = prompt;
            selectedPromptObject = prompt != null ? new SerializedObject(prompt) : null;
            GUI.FocusControl(null);
        }

        private List<QuestionPromptData> GetPreviewPromptSource()
        {
            if (previewCaseData == null)
            {
                return filteredPrompts.ToList();
            }

            return previewCaseData.AllPrompts
                .OfType<QuestionPromptData>()
                .Where(ShouldShowPrompt)
                .ToList();
        }

        private CaseRuntimeState BuildPreviewRuntime()
        {
            CaseRuntimeState runtimeState = new CaseRuntimeState();
            if (previewCaseData != null)
            {
                runtimeState.InitializeFrom(previewCaseData);
            }

            foreach (QuestionPromptData prompt in prompts)
            {
                if (prompt != null && prompt.IsStartingQuestion)
                {
                    runtimeState.AddUnlockedPrompt(prompt);
                }
            }

            return runtimeState;
        }

        private List<CharacterInteractionData> GetPreviewNpcInteractions()
        {
            if (previewNpc == null)
            {
                return new List<CharacterInteractionData>();
            }

            return interactions
                .Where(interaction => interaction != null && interaction.TargetCharacter == previewNpc)
                .ToList();
        }

        private void ValidatePrompts()
        {
            validationMessages.Clear();

            foreach (QuestionPromptData prompt in prompts)
            {
                ValidatePrompt(prompt);
            }

            ValidateDuplicateSortOrders();
        }

        private void ValidatePrompt(QuestionPromptData prompt)
        {
            if (prompt == null)
            {
                return;
            }

            if (prompt.MenuTargetType != QuestionPromptMenuTargetType.None && string.IsNullOrWhiteSpace(prompt.MenuTargetId))
            {
                validationMessages.Add(prompt.name + ": target type is set but target id is empty.");
            }

            if (prompt.MenuTargetType == QuestionPromptMenuTargetType.None && !string.IsNullOrWhiteSpace(prompt.MenuTargetId))
            {
                validationMessages.Add(prompt.name + ": target id is set but target type is None.");
            }

            if (string.IsNullOrWhiteSpace(prompt.MenuLabel) && string.IsNullOrWhiteSpace(prompt.DisplayName))
            {
                validationMessages.Add(prompt.name + ": menu label and display name are both empty.");
            }
        }

        private void ValidateDuplicateSortOrders()
        {
            IEnumerable<IGrouping<string, QuestionPromptData>> groups = prompts
                .Where(prompt => prompt != null)
                .GroupBy(prompt => prompt.MenuGroup + "|" + prompt.MenuTargetId + "|" + prompt.SortOrder.ToString());

            foreach (IGrouping<string, QuestionPromptData> group in groups)
            {
                List<QuestionPromptData> duplicatedPrompts = group.ToList();
                if (duplicatedPrompts.Count <= 1)
                {
                    continue;
                }

                string promptNames = string.Join(", ", duplicatedPrompts.Select(prompt => prompt.name));
                validationMessages.Add("Duplicate sort order in same group and target: " + promptNames);
            }
        }

        private bool IsUnconfigured(QuestionPromptData prompt)
        {
            if (prompt == null)
            {
                return false;
            }

            return prompt.MenuTargetType == QuestionPromptMenuTargetType.None
                || string.IsNullOrWhiteSpace(prompt.MenuTargetId)
                || string.IsNullOrWhiteSpace(prompt.MenuLabel);
        }

        private string GetPromptListLabel(QuestionPromptData prompt)
        {
            string id = !string.IsNullOrWhiteSpace(prompt.QuestionId) ? prompt.QuestionId : prompt.PromptId;
            string label = !string.IsNullOrWhiteSpace(prompt.MenuLabel) ? prompt.MenuLabel : prompt.DisplayName;
            string target = !string.IsNullOrWhiteSpace(prompt.MenuTargetId) ? prompt.MenuTargetId : "Unassigned";
            return id + " | " + target + " | " + label;
        }

        private void SetStringProperty(string propertyName, string value)
        {
            SerializedProperty property = selectedPromptObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            selectedPromptObject.Update();
            property.stringValue = value;
            selectedPromptObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedPrompt);
        }

        private void SetEnumProperty<TEnum>(string propertyName, TEnum value) where TEnum : Enum
        {
            SerializedProperty property = selectedPromptObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            selectedPromptObject.Update();
            property.enumValueIndex = Convert.ToInt32(value);
            selectedPromptObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedPrompt);
        }

        private string[] GetSearchFolders()
        {
            if (string.IsNullOrWhiteSpace(assetSearchFolder))
            {
                return new[] { DefaultAssetSearchFolder };
            }

            return new[] { assetSearchFolder.Trim() };
        }

        private static void LoadAssetsOfType<TAsset>(List<TAsset> assets, string[] searchFolders) where TAsset : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(TAsset).Name, searchFolders);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (asset != null && !assets.Contains(asset))
                {
                    assets.Add(asset);
                }
            }
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ComparePrompts(QuestionPromptData first, QuestionPromptData second)
        {
            if (first == null && second == null)
            {
                return 0;
            }

            if (first == null)
            {
                return 1;
            }

            if (second == null)
            {
                return -1;
            }

            int groupComparison = first.MenuGroup.CompareTo(second.MenuGroup);
            if (groupComparison != 0)
            {
                return groupComparison;
            }

            int targetComparison = string.Compare(first.MenuTargetId, second.MenuTargetId, StringComparison.OrdinalIgnoreCase);
            if (targetComparison != 0)
            {
                return targetComparison;
            }

            int orderComparison = first.SortOrder.CompareTo(second.SortOrder);
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            return string.Compare(first.name, second.name, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
