using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    public sealed class PawOrderEvidenceAuthoringWindow : EditorWindow
    {
        #region Fields

        [SerializeField]
        [Tooltip("Folder used to scan Paw Order ScriptableObjects.")]
        private string assetSearchFolder = PawOrderEvidenceAuthoringUtility.DefaultAssetSearchFolder;

        [SerializeField]
        [Tooltip("Optional database generated from blue-canary-personagens.xlsx for location and item context suggestions.")]
        private PawOrderEvidenceContextDatabase evidenceContextDatabase;

        [SerializeField]
        [Tooltip("Path to the source blue-canary-personagens.xlsx file used to generate the context database.")]
        private string truthWorkbookPath = string.Empty;

        [SerializeField]
        [Tooltip("Asset path where the generated context database will be created or updated.")]
        private string contextDatabaseAssetPath = "Assets/Project/Scriptables/Investigations/PawOrder/Editor/PawOrderEvidenceContextDatabase.asset";

        [SerializeField]
        [Tooltip("Text used to filter rows by question, speaker, answer, outcome or suspect.")]
        private string searchText = string.Empty;

        [SerializeField]
        [Tooltip("When enabled, rows without any evidence impact are shown first.")]
        private bool prioritizeEmptyEvidence = true;

        [SerializeField]
        [Tooltip("When enabled, only rows with at least one text-based suspect suggestion are shown.")]
        private bool showOnlyRowsWithSuggestions;

        [SerializeField]
        [Tooltip("When enabled, draws the selected outcome custom inspector inside this window.")]
        private bool showEmbeddedOutcomeInspector;

        private readonly List<PawOrderEvidenceAuthoringRow> rows = new List<PawOrderEvidenceAuthoringRow>();
        private readonly List<CharacterData> suspects = new List<CharacterData>();
        private readonly List<PawOrderEvidenceBatchSuggestionResult> batchPreviewResults = new List<PawOrderEvidenceBatchSuggestionResult>();
        private readonly Dictionary<InteractionOutcomeData, SuggestionCacheEntry> suggestionCache = new Dictionary<InteractionOutcomeData, SuggestionCacheEntry>();
        private readonly List<PawOrderEvidenceAuthoringRow> filteredRowsCache = new List<PawOrderEvidenceAuthoringRow>();

        private PawOrderEvidenceAuthoringRow selectedRow;
        private UnityEditor.Editor selectedOutcomeEditor;
        private Vector2 rowsScrollPosition;
        private Vector2 detailScrollPosition;
        private Vector2 batchPreviewScrollPosition;
        private bool showBatchPreview;
        private float rowsPanelWidth = 580f;
        private int suspectsSignature;
        private int cachedContextDatabaseInstanceId = int.MinValue;
        private bool filteredRowsDirty = true;
        private bool cachedSummaryDirty = true;
        private int cachedEmptyCount;
        private int cachedSuggestionCount;

        private const float MinRowsPanelWidth = 420f;
        private const float MinDetailPanelWidth = 420f;
        private static readonly List<PawOrderEvidenceSuggestion> EmptySuggestions = new List<PawOrderEvidenceSuggestion>();

        #endregion

        #region Editor

        [MenuItem("Tools/Fabula/Paw Order/Evidence Authoring")]
        public static void OpenWindow()
        {
            PawOrderEvidenceAuthoringWindow window = GetWindow<PawOrderEvidenceAuthoringWindow>();
            window.titleContent = new GUIContent("Evidence Authoring");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
        }

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            ReloadData();
        }

        private void OnDisable()
        {
            DestroyCachedEditor();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            DrawContent();
        }

        #endregion

        #region Internal Logic

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Paw & Order Evidence Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use this window to fill and refine suspect evidence impact. Suggestions can use direct suspect mentions plus optional location and item context from blue-canary-personagens.xlsx. Suggestions must be applied manually.", MessageType.Info);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                assetSearchFolder = EditorGUILayout.TextField("Asset Search Folder", assetSearchFolder);
                if (GUILayout.Button("Reload", GUILayout.Width(82f)))
                {
                    ReloadData();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                PawOrderEvidenceContextDatabase previousDatabase = evidenceContextDatabase;
                evidenceContextDatabase = (PawOrderEvidenceContextDatabase)EditorGUILayout.ObjectField("Context Database", evidenceContextDatabase, typeof(PawOrderEvidenceContextDatabase), false);
                if (previousDatabase != evidenceContextDatabase)
                {
                    MarkDerivedDataDirty();
                }

                if (GUILayout.Button("Import Truth XLSX", GUILayout.Width(126f)))
                {
                    ImportTruthWorkbook();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                truthWorkbookPath = EditorGUILayout.TextField("Truth XLSX Path", truthWorkbookPath);
                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                {
                    string selectedPath = EditorUtility.OpenFilePanel("Select blue-canary-personagens.xlsx", string.Empty, "xlsx");
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        truthWorkbookPath = selectedPath;
                    }
                }
            }

            contextDatabaseAssetPath = EditorGUILayout.TextField("Context Asset Path", contextDatabaseAssetPath);

            using (new EditorGUILayout.HorizontalScope())
            {
                string previousSearchText = searchText;
                bool previousPrioritizeEmpty = prioritizeEmptyEvidence;
                bool previousOnlySuggestions = showOnlyRowsWithSuggestions;

                searchText = EditorGUILayout.TextField("Search", searchText);
                prioritizeEmptyEvidence = EditorGUILayout.ToggleLeft("Empty First", prioritizeEmptyEvidence, GUILayout.Width(96f));
                showOnlyRowsWithSuggestions = EditorGUILayout.ToggleLeft("Only Suggestions", showOnlyRowsWithSuggestions, GUILayout.Width(128f));

                if (!string.Equals(previousSearchText, searchText, StringComparison.Ordinal)
                    || previousPrioritizeEmpty != prioritizeEmptyEvidence
                    || previousOnlySuggestions != showOnlyRowsWithSuggestions)
                {
                    MarkDerivedDataDirty();
                }
            }

            float maxRowsWidth = Mathf.Max(MinRowsPanelWidth, position.width - MinDetailPanelWidth);
            rowsPanelWidth = EditorGUILayout.Slider("Rows Width", rowsPanelWidth, MinRowsPanelWidth, maxRowsWidth);

            DrawBatchToolbar();
        }

        private void DrawBatchToolbar()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Batch Suggestion Apply", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Preview and apply safe suggestions in bulk. Safe apply only writes missing or zeroed suspect entries and never overwrites existing manual impact.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview Batch Suggestions"))
                {
                    BuildBatchPreview();
                }

                EditorGUI.BeginDisabledGroup(batchPreviewResults.Count == 0);
                if (GUILayout.Button("Apply Safe Suggestions"))
                {
                    ApplyBatchPreview();
                }

                if (GUILayout.Button("Export Batch Report"))
                {
                    ExportBatchReport();
                }
                EditorGUI.EndDisabledGroup();

                showBatchPreview = EditorGUILayout.ToggleLeft("Show Preview", showBatchPreview, GUILayout.Width(112f));
            }

            if (batchPreviewResults.Count > 0)
            {
                int safeRows = batchPreviewResults.Count(result => result.HasSafeChanges);
                int conflictRows = batchPreviewResults.Count(result => result.HasConflicts);
                int safeEntries = batchPreviewResults.Sum(result => result.SafeChanges.Count);
                EditorGUILayout.LabelField(
                    "Preview Rows: " + batchPreviewResults.Count.ToString(CultureInfo.InvariantCulture) +
                    " | Safe Rows: " + safeRows.ToString(CultureInfo.InvariantCulture) +
                    " | Safe Entries: " + safeEntries.ToString(CultureInfo.InvariantCulture) +
                    " | Conflict Rows: " + conflictRows.ToString(CultureInfo.InvariantCulture));
            }

            if (showBatchPreview)
            {
                DrawBatchPreview();
            }
        }

        private void DrawBatchPreview()
        {
            if (batchPreviewResults.Count == 0)
            {
                EditorGUILayout.HelpBox("No batch preview was generated yet.", MessageType.Info);
                return;
            }

            const float MaxPreviewHeight = 260f;
            batchPreviewScrollPosition = EditorGUILayout.BeginScrollView(batchPreviewScrollPosition, EditorStyles.helpBox, GUILayout.MaxHeight(MaxPreviewHeight));
            foreach (PawOrderEvidenceBatchSuggestionResult result in batchPreviewResults.Where(result => result.HasRelevantData))
            {
                DrawBatchPreviewResult(result);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBatchPreviewResult(PawOrderEvidenceBatchSuggestionResult result)
        {
            EditorGUILayout.LabelField(result.Row.QuestionLabel + " | " + result.Row.SpeakerLabel, EditorStyles.boldLabel);
            if (result.HasSafeChanges)
            {
                foreach (PawOrderEvidenceBatchSuggestionChange change in result.SafeChanges)
                {
                    EditorGUILayout.LabelField(
                        "Apply",
                        PawOrderEvidenceAuthoringUtility.GetCharacterLabel(change.Suspect) +
                        " | Suspicion +" + change.Score.ToString(CultureInfo.InvariantCulture) +
                        " | Motive +" + change.MotivationScore.ToString(CultureInfo.InvariantCulture) +
                        " | Means +" + change.MeansScore.ToString(CultureInfo.InvariantCulture) +
                        " | Opportunity +" + change.OpportunityScore.ToString(CultureInfo.InvariantCulture));
                }
            }

            foreach (string conflict in result.Conflicts)
            {
                EditorGUILayout.HelpBox(conflict, MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawContent()
        {
            float maxRowsWidth = Mathf.Max(MinRowsPanelWidth, position.width - MinDetailPanelWidth);
            rowsPanelWidth = Mathf.Clamp(rowsPanelWidth, MinRowsPanelWidth, maxRowsWidth);

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                DrawRowsPanel(rowsPanelWidth);
                GUILayout.Space(4f);
                DrawDetailPanel();
            }
        }

        private void DrawRowsPanel(float width)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("Interaction Outcomes", EditorStyles.boldLabel);
                DrawRowsSummary();
                DrawRowsHeader();

                rowsScrollPosition = EditorGUILayout.BeginScrollView(rowsScrollPosition);
                foreach (PawOrderEvidenceAuthoringRow row in GetFilteredRows())
                {
                    DrawRow(row);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRowsSummary()
        {
            if (cachedSummaryDirty)
            {
                cachedEmptyCount = rows.Count(row => PawOrderEvidenceAuthoringUtility.GetTotalImpact(row.Outcome) == 0 && PawOrderEvidenceAuthoringUtility.GetAxisImpact(row.Outcome) == 0);
                cachedSuggestionCount = rows.Count(row => GetSuggestions(row.Outcome).Count > 0);
                cachedSummaryDirty = false;
            }

            EditorGUILayout.LabelField(
                "Rows: " + rows.Count.ToString(CultureInfo.InvariantCulture) +
                " | Empty: " + cachedEmptyCount.ToString(CultureInfo.InvariantCulture) +
                " | With Suggestions: " + cachedSuggestionCount.ToString(CultureInfo.InvariantCulture));
        }

        private void DrawRowsHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Question / Speaker / Answer", GUILayout.MinWidth(240f));
                GUILayout.Label("Impact", GUILayout.Width(54f));
                GUILayout.Label("Suggest", GUILayout.Width(54f));
            }
        }

        private void DrawRow(PawOrderEvidenceAuthoringRow row)
        {
            bool isSelected = selectedRow != null && selectedRow.Outcome == row.Outcome;
            GUIStyle rowStyle = isSelected ? EditorStyles.helpBox : EditorStyles.textArea;
            List<PawOrderEvidenceSuggestion> rowSuggestions = GetSuggestions(row.Outcome);
            int totalImpact = PawOrderEvidenceAuthoringUtility.GetTotalImpact(row.Outcome);
            int axisImpact = PawOrderEvidenceAuthoringUtility.GetAxisImpact(row.Outcome);
            string previewText = row.QuestionLabel + " | " + row.SpeakerLabel + Environment.NewLine + Trim(row.AnswerText, 160);

            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                if (GUILayout.Button(previewText, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(38f), GUILayout.ExpandWidth(true)))
                {
                    SelectRow(row);
                }

                GUILayout.Label((totalImpact + axisImpact).ToString(CultureInfo.InvariantCulture), GUILayout.Width(54f));
                GUILayout.Label(rowSuggestions.Count.ToString(CultureInfo.InvariantCulture), GUILayout.Width(54f));
            }
        }

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("Selected Outcome", EditorStyles.boldLabel);
                detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition);

                if (selectedRow == null || selectedRow.Outcome == null)
                {
                    EditorGUILayout.HelpBox("Select a row to edit its evidence impact.", MessageType.Info);
                }
                else
                {
                    DrawSelectedContext();
                    DrawSelectedActions();
                    DrawSelectedSuggestions();
                    DrawSelectedInspector();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedContext()
        {
            EditorGUILayout.ObjectField("Outcome", selectedRow.Outcome, typeof(InteractionOutcomeData), false);
            EditorGUILayout.ObjectField("Interaction", selectedRow.Interaction, typeof(CharacterInteractionData), false);
            EditorGUILayout.LabelField("Question", selectedRow.QuestionLabel);
            EditorGUILayout.LabelField("Speaker", selectedRow.SpeakerLabel);
            EditorGUILayout.LabelField("Outcome Id", selectedRow.Outcome.OutcomeId);
            EditorGUILayout.LabelField("Answer");
            EditorGUILayout.TextArea(selectedRow.AnswerText, GUILayout.MinHeight(110f));
        }

        private void DrawSelectedActions()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Outcome"))
                {
                    EditorGUIUtility.PingObject(selectedRow.Outcome);
                    Selection.activeObject = selectedRow.Outcome;
                }

                if (GUILayout.Button("Ensure All Suspects"))
                {
                    PawOrderEvidenceAuthoringUtility.EnsureAllSuspects(selectedRow.Outcome, suspects);
                    InvalidateOutcomeSuggestion(selectedRow.Outcome);
                    MarkDerivedDataDirty();
                    ReloadSelectedEditor();
                }

                if (GUILayout.Button("Clear Evidence"))
                {
                    if (EditorUtility.DisplayDialog("Clear Evidence", "Clear every suspect evidence entry from this outcome?", "Clear", "Cancel"))
                    {
                        PawOrderEvidenceAuthoringUtility.ClearEvidence(selectedRow.Outcome);
                        InvalidateOutcomeSuggestion(selectedRow.Outcome);
                        MarkDerivedDataDirty();
                        ReloadSelectedEditor();
                    }
                }
            }
        }

        private void DrawSelectedSuggestions()
        {
            List<PawOrderEvidenceSuggestion> suggestions = GetSuggestions(selectedRow.Outcome);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Suggestions", EditorStyles.boldLabel);

            if (suggestions.Count == 0)
            {
                EditorGUILayout.HelpBox("No direct suspect, location or item context was found in this answer text.", MessageType.Info);
                return;
            }


            List<PawOrderEvidenceContextMatch> contextMatches = PawOrderEvidenceContextSuggestionService.FindContextMatches(selectedRow.Outcome, evidenceContextDatabase);
            if (contextMatches.Count > 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Matched Context", EditorStyles.boldLabel);
                foreach (PawOrderEvidenceContextMatch match in contextMatches)
                {
                    EditorGUILayout.LabelField(match.ContextType, match.ContextName + " (matched: " + match.MatchedAlias + ")");
                }
            }

            foreach (PawOrderEvidenceSuggestion suggestion in suggestions)
            {
                string label = PawOrderEvidenceAuthoringUtility.GetCharacterLabel(suggestion.Suspect) +
                    " | Suspicion +" + suggestion.Score.ToString(CultureInfo.InvariantCulture) +
                    " | Motive +" + suggestion.MotivationScore.ToString(CultureInfo.InvariantCulture) +
                    " | Means +" + suggestion.MeansScore.ToString(CultureInfo.InvariantCulture) +
                    " | Opportunity +" + suggestion.OpportunityScore.ToString(CultureInfo.InvariantCulture);
                EditorGUILayout.LabelField(label);
                EditorGUILayout.HelpBox(suggestion.Reason, MessageType.None);
            }

            if (GUILayout.Button("Apply Suggestions To Selected Outcome"))
            {
                PawOrderEvidenceAuthoringUtility.ApplySuggestions(selectedRow.Outcome, suggestions);
                InvalidateOutcomeSuggestion(selectedRow.Outcome);
                MarkDerivedDataDirty();
                ReloadSelectedEditor();
            }
        }

        private void DrawSelectedInspector()
        {
            EditorGUILayout.Space(8f);
            showEmbeddedOutcomeInspector = EditorGUILayout.Foldout(showEmbeddedOutcomeInspector, "Outcome Inspector (Embedded)", true);
            if (!showEmbeddedOutcomeInspector)
            {
                return;
            }

            if (selectedOutcomeEditor == null || selectedOutcomeEditor.target != selectedRow.Outcome)
            {
                ReloadSelectedEditor();
            }

            if (selectedOutcomeEditor != null)
            {
                selectedOutcomeEditor.OnInspectorGUI();
            }
        }

        private IEnumerable<PawOrderEvidenceAuthoringRow> GetFilteredRows()
        {
            if (!filteredRowsDirty)
            {
                return filteredRowsCache;
            }

            IEnumerable<PawOrderEvidenceAuthoringRow> filteredRows = rows;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredRows = filteredRows.Where(MatchesSearchText);
            }

            if (prioritizeEmptyEvidence)
            {
                filteredRows = filteredRows
                    .OrderBy(row => PawOrderEvidenceAuthoringUtility.GetTotalImpact(row.Outcome) == 0 && PawOrderEvidenceAuthoringUtility.GetAxisImpact(row.Outcome) == 0 ? 0 : 1)
                    .ThenBy(row => row.QuestionLabel, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.SpeakerLabel, StringComparer.OrdinalIgnoreCase);
            }

            if (showOnlyRowsWithSuggestions)
            {
                filteredRows = filteredRows.Where(row => GetSuggestions(row.Outcome).Count > 0);
            }

            filteredRowsCache.Clear();
            filteredRowsCache.AddRange(filteredRows);
            filteredRowsDirty = false;
            return filteredRowsCache;
        }

        private bool MatchesSearchText(PawOrderEvidenceAuthoringRow row)
        {
            string value = searchText ?? string.Empty;
            return Contains(row.QuestionLabel, value)
                || Contains(row.SpeakerLabel, value)
                || Contains(row.AnswerText, value)
                || Contains(row.Outcome != null ? row.Outcome.OutcomeId : string.Empty, value)
                || Contains(row.Outcome != null ? row.Outcome.name : string.Empty, value);
        }

        private bool Contains(string text, string value)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                text ?? string.Empty,
                value ?? string.Empty,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        private void SelectRow(PawOrderEvidenceAuthoringRow row)
        {
            selectedRow = row;
            ReloadSelectedEditor();
        }


        private void ImportTruthWorkbook()
        {
            try
            {
                evidenceContextDatabase = PawOrderEvidenceContextImporter.ImportFromWorkbook(truthWorkbookPath, contextDatabaseAssetPath);
                EditorUtility.DisplayDialog(
                    "Evidence Context Imported",
                    "The evidence context database was created or updated successfully.",
                    "OK");
                ReloadData();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Evidence Context Import Failed", exception.Message, "OK");
            }
        }

        private void BuildBatchPreview()
        {
            batchPreviewResults.Clear();
            batchPreviewResults.AddRange(PawOrderEvidenceBatchSuggestionApplier.BuildPreview(rows, suspects, evidenceContextDatabase));
            showBatchPreview = true;
            Repaint();
        }

        private void ApplyBatchPreview()
        {
            if (batchPreviewResults.Count == 0)
            {
                return;
            }

            int safeRows = batchPreviewResults.Count(result => result.HasSafeChanges);
            int safeEntries = batchPreviewResults.Sum(result => result.SafeChanges.Count);
            bool approved = EditorUtility.DisplayDialog(
                "Apply Safe Suggestions",
                "This will apply " + safeEntries.ToString(CultureInfo.InvariantCulture) + " safe suspect evidence entries across " + safeRows.ToString(CultureInfo.InvariantCulture) + " outcomes. Existing manual impact will not be overwritten.",
                "Apply",
                "Cancel");

            if (!approved)
            {
                return;
            }

            PawOrderEvidenceBatchApplySummary summary = PawOrderEvidenceBatchSuggestionApplier.ApplySafeSuggestions(batchPreviewResults);
            ReloadData();
            ReloadSelectedEditor();
            BuildBatchPreview();
            EditorUtility.DisplayDialog(
                "Batch Suggestions Applied",
                "Changed Rows: " + summary.ChangedRows.ToString(CultureInfo.InvariantCulture) +
                "\nApplied Entries: " + summary.AppliedEntries.ToString(CultureInfo.InvariantCulture) +
                "\nConflict Rows Skipped: " + summary.ConflictRows.ToString(CultureInfo.InvariantCulture),
                "OK");
        }

        private void ExportBatchReport()
        {
            if (batchPreviewResults.Count == 0)
            {
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export Batch Suggestion Report",
                Application.dataPath,
                "PawOrderEvidenceBatchSuggestionReport.txt",
                "txt");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            System.IO.File.WriteAllText(path, PawOrderEvidenceBatchSuggestionApplier.BuildReport(batchPreviewResults));
            EditorUtility.RevealInFinder(path);
        }

        private void ReloadData()
        {
            suspects.Clear();
            rows.Clear();
            batchPreviewResults.Clear();
            suggestionCache.Clear();
            filteredRowsCache.Clear();
            cachedContextDatabaseInstanceId = int.MinValue;
            suspects.AddRange(PawOrderEvidenceAuthoringUtility.LoadSuspects(assetSearchFolder));
            suspectsSignature = ComputeSuspectsSignature();

            List<CharacterInteractionData> interactions = PawOrderEvidenceAuthoringUtility.LoadInteractions(assetSearchFolder);
            HashSet<InteractionOutcomeData> mappedOutcomes = new HashSet<InteractionOutcomeData>();
            foreach (CharacterInteractionData interaction in interactions)
            {
                if (interaction.Outcome == null)
                {
                    continue;
                }

                mappedOutcomes.Add(interaction.Outcome);
                rows.Add(new PawOrderEvidenceAuthoringRow(interaction));
            }

            foreach (InteractionOutcomeData outcome in PawOrderEvidenceAuthoringUtility.LoadOutcomes(assetSearchFolder))
            {
                if (mappedOutcomes.Contains(outcome))
                {
                    continue;
                }

                rows.Add(new PawOrderEvidenceAuthoringRow(outcome));
            }

            rows.Sort((left, right) => string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase));

            if (selectedRow != null && !rows.Any(row => row.Outcome == selectedRow.Outcome))
            {
                selectedRow = null;
                DestroyCachedEditor();
            }

            MarkDerivedDataDirty();
            Repaint();
        }

        private void ReloadSelectedEditor()
        {
            DestroyCachedEditor();
            if (selectedRow != null && selectedRow.Outcome != null)
            {
                selectedOutcomeEditor = UnityEditor.Editor.CreateEditor(selectedRow.Outcome);
            }
        }

        private void DestroyCachedEditor()
        {
            if (selectedOutcomeEditor != null)
            {
                DestroyImmediate(selectedOutcomeEditor);
                selectedOutcomeEditor = null;
            }
        }

        private string Trim(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, maxLength - 3) + "...";
        }

        private List<PawOrderEvidenceSuggestion> GetSuggestions(InteractionOutcomeData outcome)
        {
            if (outcome == null)
            {
                return EmptySuggestions;
            }

            EnsureSuggestionCacheState();

            string responseText = outcome.ResponseText ?? string.Empty;
            int contextDatabaseInstanceId = evidenceContextDatabase != null ? evidenceContextDatabase.GetInstanceID() : 0;
            if (suggestionCache.TryGetValue(outcome, out SuggestionCacheEntry entry)
                && entry.ResponseText == responseText
                && entry.SuspectsSignature == suspectsSignature
                && entry.ContextDatabaseInstanceId == contextDatabaseInstanceId)
            {
                return entry.Suggestions;
            }

            List<PawOrderEvidenceSuggestion> suggestions = PawOrderEvidenceAuthoringUtility.BuildSuggestions(outcome, suspects, evidenceContextDatabase);
            suggestionCache[outcome] = new SuggestionCacheEntry(responseText, suspectsSignature, contextDatabaseInstanceId, suggestions);
            return suggestions;
        }

        private void EnsureSuggestionCacheState()
        {
            int contextDatabaseInstanceId = evidenceContextDatabase != null ? evidenceContextDatabase.GetInstanceID() : 0;
            if (contextDatabaseInstanceId == cachedContextDatabaseInstanceId)
            {
                return;
            }

            suggestionCache.Clear();
            cachedContextDatabaseInstanceId = contextDatabaseInstanceId;
            MarkDerivedDataDirty();
        }

        private void InvalidateOutcomeSuggestion(InteractionOutcomeData outcome)
        {
            if (outcome == null)
            {
                return;
            }

            suggestionCache.Remove(outcome);
        }

        private void MarkDerivedDataDirty()
        {
            filteredRowsDirty = true;
            cachedSummaryDirty = true;
        }

        private int ComputeSuspectsSignature()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < suspects.Count; i++)
                {
                    CharacterData suspect = suspects[i];
                    hash = (hash * 31) + (suspect != null ? suspect.GetInstanceID() : 0);
                }

                return hash;
            }
        }


        #endregion
    }

    internal sealed class PawOrderEvidenceAuthoringRow
    {
        #region Properties

        internal CharacterInteractionData Interaction { get; }
        internal InteractionOutcomeData Outcome { get; }
        internal string QuestionLabel { get; }
        internal string SpeakerLabel { get; }
        internal string AnswerText { get; }
        internal string SortKey { get; }

        #endregion

        #region Public API

        internal PawOrderEvidenceAuthoringRow(CharacterInteractionData interaction)
        {
            Interaction = interaction;
            Outcome = interaction != null ? interaction.Outcome : null;
            QuestionLabel = interaction != null ? PawOrderEvidenceAuthoringUtility.GetPromptLabel(interaction.PromptUsed) : "No Question";
            SpeakerLabel = interaction != null ? PawOrderEvidenceAuthoringUtility.GetCharacterLabel(interaction.TargetCharacter) : "No Speaker";
            AnswerText = Outcome != null ? Outcome.ResponseText : string.Empty;
            SortKey = QuestionLabel + "_" + SpeakerLabel + "_" + (Outcome != null ? Outcome.name : string.Empty);
        }

        internal PawOrderEvidenceAuthoringRow(InteractionOutcomeData outcome)
        {
            Interaction = null;
            Outcome = outcome;
            QuestionLabel = "Unmapped";
            SpeakerLabel = "Unknown";
            AnswerText = outcome != null ? outcome.ResponseText : string.Empty;
            SortKey = QuestionLabel + "_" + SpeakerLabel + "_" + (outcome != null ? outcome.name : string.Empty);
        }

        #endregion
    }

    internal readonly struct SuggestionCacheEntry
    {
        internal string ResponseText { get; }
        internal int SuspectsSignature { get; }
        internal int ContextDatabaseInstanceId { get; }
        internal List<PawOrderEvidenceSuggestion> Suggestions { get; }

        internal SuggestionCacheEntry(
            string responseText,
            int suspectsSignature,
            int contextDatabaseInstanceId,
            List<PawOrderEvidenceSuggestion> suggestions)
        {
            ResponseText = responseText;
            SuspectsSignature = suspectsSignature;
            ContextDatabaseInstanceId = contextDatabaseInstanceId;
            Suggestions = suggestions;
        }
    }
}
