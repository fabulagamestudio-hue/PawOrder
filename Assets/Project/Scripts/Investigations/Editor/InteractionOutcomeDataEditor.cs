using System.Collections.Generic;
using System.Globalization;
using Fabula.PawOrder;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    [CustomEditor(typeof(InteractionOutcomeData))]
    public sealed class InteractionOutcomeDataEditor : UnityEditor.Editor
    {
        #region Fields

        [SerializeField]
        [Tooltip("Folder used to scan suspect CharacterData assets.")]
        private string assetSearchFolder = PawOrderEvidenceAuthoringUtility.DefaultAssetSearchFolder;

        [SerializeField]
        [Tooltip("Optional database generated from blue-canary-personagens.xlsx for location and item context suggestions.")]
        private PawOrderEvidenceContextDatabase evidenceContextDatabase;

        private readonly List<CharacterData> suspects = new List<CharacterData>();
        private bool showAuthoringTools = true;

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            ReloadSuspects();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            DrawAuthoringTools();
        }

        #endregion

        #region Internal Logic

        private void DrawAuthoringTools()
        {
            InteractionOutcomeData outcome = target as InteractionOutcomeData;
            if (outcome == null)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            showAuthoringTools = EditorGUILayout.Foldout(showAuthoringTools, "Evidence Authoring Tools", true);
            if (!showAuthoringTools)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawConfiguration();
                DrawImpactSummary(outcome);
                DrawSuggestions(outcome);
                DrawActions(outcome);
            }
        }

        private void DrawConfiguration()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                assetSearchFolder = EditorGUILayout.TextField("Asset Search Folder", assetSearchFolder);
                if (GUILayout.Button("Reload", GUILayout.Width(72f)))
                {
                    ReloadSuspects();
                }
            }

            evidenceContextDatabase = (PawOrderEvidenceContextDatabase)EditorGUILayout.ObjectField("Context Database", evidenceContextDatabase, typeof(PawOrderEvidenceContextDatabase), false);
        }

        private void DrawImpactSummary(InteractionOutcomeData outcome)
        {
            int totalImpact = PawOrderEvidenceAuthoringUtility.GetTotalImpact(outcome);
            int axisImpact = PawOrderEvidenceAuthoringUtility.GetAxisImpact(outcome);
            EditorGUILayout.LabelField("Current Impact", "suspicion=" + totalImpact.ToString(CultureInfo.InvariantCulture) + " | axes=" + axisImpact.ToString(CultureInfo.InvariantCulture));
        }

        private void DrawSuggestions(InteractionOutcomeData outcome)
        {
            List<PawOrderEvidenceSuggestion> suggestions = PawOrderEvidenceAuthoringUtility.BuildSuggestions(outcome, suspects, evidenceContextDatabase);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Text Suggestions", EditorStyles.boldLabel);

            if (suggestions.Count == 0)
            {
                EditorGUILayout.HelpBox("No direct suspect, location or item context was found in the answer text.", MessageType.Info);
                return;
            }

            foreach (PawOrderEvidenceSuggestion suggestion in suggestions)
            {
                EditorGUILayout.LabelField(
                    PawOrderEvidenceAuthoringUtility.GetCharacterLabel(suggestion.Suspect),
                    "Suspicion +" + suggestion.Score.ToString(CultureInfo.InvariantCulture) +
                    " | Motive +" + suggestion.MotivationScore.ToString(CultureInfo.InvariantCulture) +
                    " | Means +" + suggestion.MeansScore.ToString(CultureInfo.InvariantCulture) +
                    " | Opportunity +" + suggestion.OpportunityScore.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.HelpBox(suggestion.Reason, MessageType.None);
            }

            if (GUILayout.Button("Apply Suggestions"))
            {
                PawOrderEvidenceAuthoringUtility.ApplySuggestions(outcome, suggestions);
                serializedObject.Update();
            }
        }

        private void DrawActions(InteractionOutcomeData outcome)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ensure All Suspects"))
                {
                    PawOrderEvidenceAuthoringUtility.EnsureAllSuspects(outcome, suspects);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Open Authoring Window"))
                {
                    PawOrderEvidenceAuthoringWindow.OpenWindow();
                }
            }

            if (GUILayout.Button("Clear Evidence"))
            {
                if (EditorUtility.DisplayDialog("Clear Evidence", "Clear every suspect evidence entry from this outcome?", "Clear", "Cancel"))
                {
                    PawOrderEvidenceAuthoringUtility.ClearEvidence(outcome);
                    serializedObject.Update();
                }
            }
        }

        private void ReloadSuspects()
        {
            suspects.Clear();
            suspects.AddRange(PawOrderEvidenceAuthoringUtility.LoadSuspects(assetSearchFolder));
        }

        #endregion
    }
}
