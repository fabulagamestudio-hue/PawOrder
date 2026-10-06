using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    public sealed class PawOrderEvidenceImpactValidatorWindow : EditorWindow
    {
        #region Fields

        private const string DefaultAssetSearchFolder = "Assets/Project/Scriptables/Investigations/PawOrder";
        private const string DefaultReportFolder = "Assets/Project/Scriptables/Investigations/PawOrder/Reports";
        private const string MarkdownReportName = "EvidenceImpactValidationReport.md";
        private const string CsvReportName = "EvidenceImpactValidationReport.csv";

        [SerializeField]
        [Tooltip("Folder used by AssetDatabase.FindAssets to scan Paw Order ScriptableObjects.")]
        private string assetSearchFolder = DefaultAssetSearchFolder;

        [SerializeField]
        [Tooltip("Path to the .xlsx file that contains the canonical truth of the mystery.")]
        private string truthWorkbookPath = string.Empty;

        [SerializeField]
        [Tooltip("Path to the .xlsx file that contains the final question and answer map.")]
        private string questionnaireWorkbookPath = string.Empty;

        private readonly PawOrderEvidenceImpactValidatorService validatorService = new PawOrderEvidenceImpactValidatorService();
        private readonly Dictionary<string, bool> issueFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private PawOrderEvidenceImpactReport currentReport;
        private Vector2 scrollPosition;
        private int selectedAssetIndex = -1;

        #endregion

        #region Editor

        [MenuItem("Tools/Fabula/Paw Order/Evidence Impact Validator")]
        public static void OpenWindow()
        {
            PawOrderEvidenceImpactValidatorWindow window = GetWindow<PawOrderEvidenceImpactValidatorWindow>();
            window.titleContent = new GUIContent("Impact Validator");
            window.minSize = new Vector2(820f, 560f);
            window.Show();
        }

        #endregion

        #region Unity Messages

        private void OnGUI()
        {
            DrawHeader();
            DrawConfiguration();
            DrawActions();
            DrawAssetPicker();
            DrawReport();
        }

        #endregion

        #region Internal Logic

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Paw & Order Evidence Impact Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Validates how much each answer incriminates each suspect. The truth workbook is used as narrative support, not as an automatic correction of evidence scores.", MessageType.Info);
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            assetSearchFolder = EditorGUILayout.TextField("Asset Search Folder", assetSearchFolder);
            DrawWorkbookField("Truth Workbook", ref truthWorkbookPath);
            DrawWorkbookField("Question Workbook", ref questionnaireWorkbookPath);
        }

        private void DrawWorkbookField(string label, ref string path)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = EditorGUILayout.TextField(label, path);
                if (GUILayout.Button("Select", GUILayout.Width(72f)))
                {
                    string selectedPath = EditorUtility.OpenFilePanel(label, Application.dataPath, "xlsx");
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        path = selectedPath;
                    }
                }
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run Validation", GUILayout.Height(28f)))
                {
                    RunValidation();
                }

                using (new EditorGUI.DisabledScope(currentReport == null || !currentReport.HasData))
                {
                    if (GUILayout.Button("Generate Markdown", GUILayout.Height(28f)))
                    {
                        GenerateMarkdownReport();
                    }

                    if (GUILayout.Button("Generate CSV", GUILayout.Height(28f)))
                    {
                        GenerateCsvReport();
                    }
                }
            }
        }

        private void DrawAssetPicker()
        {
            if (currentReport == null || currentReport.ReferencedAssets.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            if (selectedAssetIndex < 0 || selectedAssetIndex >= currentReport.ReferencedAssets.Count)
            {
                selectedAssetIndex = 0;
            }

            string[] labels = currentReport.ReferencedAssets.Select(GetAssetLabel).ToArray();
            selectedAssetIndex = EditorGUILayout.Popup("Referenced Asset", selectedAssetIndex, labels);
            if (GUILayout.Button("Ping Selected Asset"))
            {
                UnityEngine.Object selectedAsset = currentReport.ReferencedAssets[selectedAssetIndex];
                EditorGUIUtility.PingObject(selectedAsset);
                Selection.activeObject = selectedAsset;
            }
        }

        private void DrawReport()
        {
            EditorGUILayout.Space(8f);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (currentReport == null)
            {
                EditorGUILayout.HelpBox("Run the validation to generate the first report.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawSummary();
            EditorGUILayout.Space(8f);
            DrawSuspectSummary();
            EditorGUILayout.Space(8f);
            DrawIssues();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Evidence Rows", currentReport.Rows.Count.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Issues", currentReport.Issues.Count.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Errors", currentReport.Issues.Count(issue => issue.Severity == PawOrderEvidenceImpactSeverity.Error).ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Warnings", currentReport.Issues.Count(issue => issue.Severity == PawOrderEvidenceImpactSeverity.Warning).ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Info", currentReport.Issues.Count(issue => issue.Severity == PawOrderEvidenceImpactSeverity.Info).ToString(CultureInfo.InvariantCulture));
        }

        private void DrawSuspectSummary()
        {
            if (currentReport.Rows.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Top Suspects by Evidence Impact", EditorStyles.boldLabel);
            foreach (IGrouping<string, PawOrderEvidenceImpactRow> suspectGroup in currentReport.Rows.GroupBy(row => row.SuspectName).OrderByDescending(group => group.Sum(row => row.Score)).Take(8))
            {
                int totalScore = suspectGroup.Sum(row => row.Score);
                int motivationScore = suspectGroup.Sum(row => row.MotivationScore);
                int meansScore = suspectGroup.Sum(row => row.MeansScore);
                int opportunityScore = suspectGroup.Sum(row => row.OpportunityScore);
                EditorGUILayout.LabelField(suspectGroup.Key, "total=" + totalScore + " | motivation=" + motivationScore + " | means=" + meansScore + " | opportunity=" + opportunityScore);
            }
        }

        private void DrawIssues()
        {
            EditorGUILayout.LabelField("Issues", EditorStyles.boldLabel);
            if (currentReport.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues found.", MessageType.Info);
                return;
            }

            foreach (IGrouping<string, PawOrderEvidenceImpactIssue> categoryGroup in currentReport.Issues.GroupBy(issue => issue.Category).OrderByDescending(group => group.Count()))
            {
                string foldoutKey = categoryGroup.Key;
                bool expanded = issueFoldouts.TryGetValue(foldoutKey, out bool previousState) && previousState;
                expanded = EditorGUILayout.Foldout(expanded, categoryGroup.Key + " (" + categoryGroup.Count() + ")", true);
                issueFoldouts[foldoutKey] = expanded;
                if (!expanded)
                {
                    continue;
                }

                foreach (PawOrderEvidenceImpactIssue issue in categoryGroup.Take(30))
                {
                    DrawIssue(issue);
                }

                int hiddenCount = categoryGroup.Count() - 30;
                if (hiddenCount > 0)
                {
                    EditorGUILayout.LabelField("... +" + hiddenCount + " hidden issues. Export the Markdown report to review all entries.");
                }
            }
        }

        private void DrawIssue(PawOrderEvidenceImpactIssue issue)
        {
            MessageType messageType = issue.Severity == PawOrderEvidenceImpactSeverity.Error
                ? MessageType.Error
                : issue.Severity == PawOrderEvidenceImpactSeverity.Warning
                    ? MessageType.Warning
                    : MessageType.Info;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("[" + issue.Severity + "] " + issue.Message, messageType);
                if (issue.Context != null && GUILayout.Button("Ping Context", GUILayout.Width(110f)))
                {
                    EditorGUIUtility.PingObject(issue.Context);
                    Selection.activeObject = issue.Context;
                }
            }
        }

        private void RunValidation()
        {
            currentReport = validatorService.Validate(truthWorkbookPath, questionnaireWorkbookPath, assetSearchFolder);
            selectedAssetIndex = currentReport.ReferencedAssets.Count > 0 ? 0 : -1;
            issueFoldouts.Clear();
            Debug.Log("[PawOrderEvidenceImpactValidatorWindow] Validation completed. Rows=" + currentReport.Rows.Count + ", Issues=" + currentReport.Issues.Count);
        }

        private void GenerateMarkdownReport()
        {
            if (currentReport == null)
            {
                return;
            }

            EnsureReportFolder();
            string path = Path.Combine(DefaultReportFolder, MarkdownReportName).Replace('\\', '/');
            File.WriteAllText(path, currentReport.BuildMarkdown(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("[PawOrderEvidenceImpactValidatorWindow] Markdown report generated: " + path);
        }

        private void GenerateCsvReport()
        {
            if (currentReport == null)
            {
                return;
            }

            EnsureReportFolder();
            string path = Path.Combine(DefaultReportFolder, CsvReportName).Replace('\\', '/');
            File.WriteAllText(path, currentReport.BuildCsv(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("[PawOrderEvidenceImpactValidatorWindow] CSV report generated: " + path);
        }

        private static void EnsureReportFolder()
        {
            if (AssetDatabase.IsValidFolder(DefaultReportFolder))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Project/Scriptables/Investigations/PawOrder"))
            {
                Debug.LogWarning("[PawOrderEvidenceImpactValidatorWindow] Default Paw Order folder does not exist. Report folder was not created.");
                return;
            }

            AssetDatabase.CreateFolder("Assets/Project/Scriptables/Investigations/PawOrder", "Reports");
        }

        private static string GetAssetLabel(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return "(missing)";
            }

            return asset.GetType().Name + ": " + asset.name;
        }

        #endregion
    }
}
