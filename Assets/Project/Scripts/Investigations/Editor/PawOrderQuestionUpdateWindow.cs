using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    public sealed class PawOrderQuestionUpdateWindow : EditorWindow
    {
        #region Fields

        [SerializeField]
        [Tooltip("Workbook path containing only the new or updated question rows.")]
        private string workbookPath = string.Empty;

        [SerializeField]
        [Tooltip("Root asset folder used by the Paw & Order investigation ScriptableObjects.")]
        private string rootFolder = "Assets/Project/Scriptables/Investigations/PawOrder";

        [SerializeField]
        [Tooltip("Allows existing outcome response text to be compared and reported without being overwritten.")]
        private bool warnWhenExistingResponseDiffers = true;

        private Vector2 scrollPosition;
        private PawOrderQuestionUpdateReport lastReport;

        #endregion

        #region Unity Messages

        [MenuItem("Fabula/Paw Order/Investigations/Update New Questions")]
        public static void Open()
        {
            PawOrderQuestionUpdateWindow window = GetWindow<PawOrderQuestionUpdateWindow>("Paw Order Question Update");
            window.minSize = new Vector2(620f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawSettings();
            DrawActions();
            DrawReport();
        }

        #endregion

        #region Internal Logic

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Paw & Order - Incremental Question Update", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use this window to import only new questions and missing character responses. Existing InteractionOutcomeData assets are never overwritten, so Evidence Authoring configuration is preserved.",
                MessageType.Info);
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                workbookPath = EditorGUILayout.TextField("Workbook", workbookPath);
                if (GUILayout.Button("Browse", GUILayout.Width(90f)))
                {
                    string selectedPath = EditorUtility.OpenFilePanel("Select Questions Workbook", Application.dataPath, "xlsx");
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        workbookPath = selectedPath;
                    }
                }
            }

            rootFolder = EditorGUILayout.TextField("Root Folder", rootFolder);
            warnWhenExistingResponseDiffers = EditorGUILayout.ToggleLeft("Report changed existing responses without overwriting them", warnWhenExistingResponseDiffers);

            if (!string.IsNullOrWhiteSpace(workbookPath) && !File.Exists(workbookPath))
            {
                EditorGUILayout.HelpBox("Workbook file was not found.", MessageType.Warning);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview Update", GUILayout.Height(30f)))
                {
                    RunPreview();
                }

                GUI.enabled = lastReport != null || File.Exists(workbookPath);
                if (GUILayout.Button("Apply Update", GUILayout.Height(30f)))
                {
                    RunApply();
                }
                GUI.enabled = true;
            }
        }

        private void DrawReport()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);

            if (lastReport == null)
            {
                EditorGUILayout.HelpBox("No preview or update has been executed yet.", MessageType.None);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("Created Questions", lastReport.CreatedQuestions.ToString());
            EditorGUILayout.LabelField("Created Outcomes", lastReport.CreatedOutcomes.ToString());
            EditorGUILayout.LabelField("Created Interactions", lastReport.CreatedInteractions.ToString());
            EditorGUILayout.LabelField("Existing Questions", lastReport.ExistingQuestions.ToString());
            EditorGUILayout.LabelField("Existing Outcomes", lastReport.ExistingOutcomes.ToString());
            EditorGUILayout.LabelField("Existing Interactions", lastReport.ExistingInteractions.ToString());

            if (warnWhenExistingResponseDiffers)
            {
                EditorGUILayout.LabelField("Skipped Changed Responses", lastReport.SkippedChangedResponses.ToString());
            }

            DrawReportSection("Messages", lastReport.Messages, MessageType.Info);
            DrawReportSection("Warnings", warnWhenExistingResponseDiffers ? lastReport.Warnings : System.Array.Empty<string>(), MessageType.Warning);
            DrawReportSection("Created Assets", lastReport.CreatedAssetPaths, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawReportSection(string title, System.Collections.Generic.IReadOnlyList<string> values, MessageType messageType)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string value in values)
            {
                if (messageType == MessageType.None)
                {
                    EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                else
                {
                    EditorGUILayout.HelpBox(value, messageType);
                }
            }
        }

        private void RunPreview()
        {
            PawOrderQuestionUpdateService service = new PawOrderQuestionUpdateService(rootFolder);
            lastReport = service.PreviewUpdate(workbookPath);
        }

        private void RunApply()
        {
            if (!EditorUtility.DisplayDialog(
                "Apply Incremental Question Update",
                "This will create only missing question, outcome and interaction assets. Existing configured outcomes will not be overwritten.",
                "Apply",
                "Cancel"))
            {
                return;
            }

            PawOrderQuestionUpdateService service = new PawOrderQuestionUpdateService(rootFolder);
            lastReport = service.ApplyUpdate(workbookPath);
        }

        #endregion
    }
}
