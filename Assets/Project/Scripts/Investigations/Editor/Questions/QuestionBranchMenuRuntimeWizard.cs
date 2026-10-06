using Fabula.PawOrder;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fabula.PawOrder.Editor
{
    public sealed class QuestionBranchMenuRuntimeWizard : EditorWindow
    {
        #region Constants

        private const string MenuPath = "Fabula/Paw Order/Create Question Branch Menu Runtime UI";
        private const string WindowTitle = "Question Branch Menu";
        private const string RootName = "Question Branch Menu Runtime UI";
        private const float DefaultPanelWidth = 820f;
        private const float DefaultPanelHeight = 430f;

        #endregion

        #region Fields

        [SerializeField]
        [Tooltip("Canvas that will receive the runtime question menu UI.")]
        private Canvas targetCanvas;

        [SerializeField]
        [Tooltip("Optional parent used to place the runtime question menu UI. When empty, the target canvas transform is used.")]
        private RectTransform parentRoot;

        [SerializeField]
        [Tooltip("Existing option prefab used by the runtime question branch menu.")]
        private QuestionBranchMenuOptionView optionTemplate;

        [SerializeField]
        [Tooltip("Case data assigned to the generated runtime connector.")]
        private CaseData caseData;

        [SerializeField]
        [Tooltip("Creates a response overlay that displays the selected interaction outcome.")]
        private bool createResponseView = true;

        [SerializeField]
        [Tooltip("Creates a runtime connector and links existing character interaction controllers automatically.")]
        private bool createRuntimeConnector = true;

        [SerializeField]
        [Tooltip("Adds a RectMask2D to the columns viewport.")]
        private bool useRectMask = true;

        [SerializeField]
        [Tooltip("Initial fixed size applied to every runtime branch column and its vertical scroll viewport.")]
        private Vector2 columnSize = new Vector2(214f, 260f);

        [SerializeField]
        [Tooltip("Fixed size applied to every option RectTransform.")]
        private Vector2 optionSize = new Vector2(180f, 42f);

        [SerializeField]
        [Tooltip("Horizontal distance between each depth column.")]
        private float columnSpacing = 210f;

        [SerializeField]
        [Tooltip("Vertical distance between options in the same column.")]
        private float rowSpacing = 8f;

        [SerializeField]
        [Tooltip("Left offset applied to inactive sibling options after a branch is selected.")]
        private float inactiveOptionOffset = 34f;

        [SerializeField]
        [Tooltip("Left offset applied to older columns as the active branch advances.")]
        private float inactiveColumnOffset = 18f;

        [SerializeField]
        [Tooltip("Horizontal offset used when new options enter from the right.")]
        private float enterFromRightOffset = 42f;

        [SerializeField]
        [Tooltip("Scroll sensitivity applied to every vertical column ScrollRect.")]
        private float columnScrollSensitivity = 18f;

        [SerializeField]
        [Tooltip("Fade duration used when the full menu is shown or hidden.")]
        private float fadeDuration = 0.15f;

        [SerializeField]
        [Tooltip("Duration used when a column moves between active and inactive positions.")]
        private float columnMoveDuration = 0.18f;

        [SerializeField]
        [Tooltip("Duration used by each option when it appears or changes state.")]
        private float optionTweenDuration = 0.18f;

        [SerializeField]
        [Tooltip("Delay applied between each option while a column appears.")]
        private float optionStaggerDelay = 0.035f;

        [SerializeField]
        [Tooltip("Scale used by inactive sibling options.")]
        private float inactiveOptionScale = 0.96f;

        [SerializeField]
        [Tooltip("Alpha used by inactive sibling options.")]
        private float inactiveOptionAlpha = 0.72f;

        private Vector2 scrollPosition;

        #endregion

        #region Public API

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            QuestionBranchMenuRuntimeWizard window = GetWindow<QuestionBranchMenuRuntimeWizard>(WindowTitle);
            window.minSize = new Vector2(420f, 560f);
            window.Show();
        }

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            if (targetCanvas == null)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawSceneReferences();
            DrawLayoutSettings();
            DrawTweenSettings();
            DrawCreateButton();

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Internal Logic

        private void DrawSceneReferences()
        {
            EditorGUILayout.LabelField("Scene References", EditorStyles.boldLabel);
            targetCanvas = (Canvas)EditorGUILayout.ObjectField("Target Canvas", targetCanvas, typeof(Canvas), true);
            parentRoot = (RectTransform)EditorGUILayout.ObjectField("Parent Root", parentRoot, typeof(RectTransform), true);
            optionTemplate = (QuestionBranchMenuOptionView)EditorGUILayout.ObjectField("Option Prefab", optionTemplate, typeof(QuestionBranchMenuOptionView), false);
            caseData = (CaseData)EditorGUILayout.ObjectField("Case Data", caseData, typeof(CaseData), false);
            createResponseView = EditorGUILayout.Toggle("Create Response View", createResponseView);
            createRuntimeConnector = EditorGUILayout.Toggle("Create Runtime Connector", createRuntimeConnector);
            useRectMask = EditorGUILayout.Toggle("Use Rect Mask", useRectMask);
            EditorGUILayout.Space(8f);
        }

        private void DrawLayoutSettings()
        {
            EditorGUILayout.LabelField("Manual Layout", EditorStyles.boldLabel);
            columnSize = EditorGUILayout.Vector2Field("Column Size", columnSize);
            optionSize = EditorGUILayout.Vector2Field("Option Size", optionSize);
            columnSpacing = EditorGUILayout.FloatField("Column Spacing", columnSpacing);
            rowSpacing = EditorGUILayout.FloatField("Row Spacing", rowSpacing);
            inactiveOptionOffset = EditorGUILayout.FloatField("Inactive Option Offset", inactiveOptionOffset);
            inactiveColumnOffset = EditorGUILayout.FloatField("Inactive Column Offset", inactiveColumnOffset);
            enterFromRightOffset = EditorGUILayout.FloatField("Enter From Right Offset", enterFromRightOffset);
            columnScrollSensitivity = EditorGUILayout.FloatField("Column Scroll Sensitivity", columnScrollSensitivity);
            EditorGUILayout.Space(8f);
        }

        private void DrawTweenSettings()
        {
            EditorGUILayout.LabelField("DoTween", EditorStyles.boldLabel);
            fadeDuration = EditorGUILayout.FloatField("Fade Duration", fadeDuration);
            columnMoveDuration = EditorGUILayout.FloatField("Column Move Duration", columnMoveDuration);
            optionTweenDuration = EditorGUILayout.FloatField("Option Tween Duration", optionTweenDuration);
            optionStaggerDelay = EditorGUILayout.FloatField("Option Stagger Delay", optionStaggerDelay);
            inactiveOptionScale = EditorGUILayout.FloatField("Inactive Option Scale", inactiveOptionScale);
            inactiveOptionAlpha = EditorGUILayout.Slider("Inactive Option Alpha", inactiveOptionAlpha, 0f, 1f);
            EditorGUILayout.Space(12f);
        }

        private void DrawCreateButton()
        {
            using (new EditorGUI.DisabledScope(!CanCreateUi()))
            {
                if (GUILayout.Button("Create Runtime Branch Menu UI", GUILayout.Height(34f)))
                {
                    CreateRuntimeBranchMenuUi();
                }
            }

            if (optionTemplate == null)
            {
                EditorGUILayout.HelpBox("Assign an existing option prefab with QuestionBranchMenuOptionView before creating the UI.", MessageType.Warning);
            }
        }

        private bool CanCreateUi()
        {
            return targetCanvas != null && optionTemplate != null;
        }

        private void CreateRuntimeBranchMenuUi()
        {
            EnsureEventSystem();

            Transform parent = parentRoot != null
                ? parentRoot
                : targetCanvas.transform;

            GameObject rootObject = CreateUiObject(RootName, parent);
            RectTransform rootRectTransform = rootObject.transform as RectTransform;
            ConfigureCenteredPanel(rootRectTransform);

            CanvasGroup rootCanvasGroup = rootObject.AddComponent<CanvasGroup>();
            Image backgroundImage = rootObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.08f, 0.09f, 0.96f);

            TMP_Text titleText = CreateText("Title", rootObject.transform, "Question Menu", 28, FontStyles.Bold);
            RectTransform titleRectTransform = titleText.transform as RectTransform;
            ConfigureHeaderText(titleRectTransform, -24f, 42f);

            TMP_Text subtitleText = CreateText("Subtitle", rootObject.transform, "Current NPC: none", 18, FontStyles.Normal);
            RectTransform subtitleRectTransform = subtitleText.transform as RectTransform;
            ConfigureHeaderText(subtitleRectTransform, -68f, 30f);

            GameObject viewportObject = CreateUiObject("Columns Viewport", rootObject.transform);
            RectTransform viewportRectTransform = viewportObject.transform as RectTransform;
            ConfigureColumnsViewport(viewportRectTransform);

            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

            if (useRectMask)
            {
                viewportObject.AddComponent<RectMask2D>();
            }

            GameObject columnsRootObject = CreateUiObject("Columns Root", viewportObject.transform);
            RectTransform columnsRoot = columnsRootObject.transform as RectTransform;
            ConfigureColumnsRoot(columnsRoot);

            QuestionBranchMenuRuntimeView menuView = rootObject.AddComponent<QuestionBranchMenuRuntimeView>();
            menuView.ConfigureForEditor(rootCanvasGroup, titleText, subtitleText, columnsRoot, optionTemplate);
            menuView.ConfigureLayoutForEditor(
                columnSize,
                optionSize,
                columnSpacing,
                rowSpacing,
                inactiveOptionOffset,
                inactiveColumnOffset,
                enterFromRightOffset,
                columnScrollSensitivity);
            menuView.ConfigureTweenForEditor(fadeDuration, columnMoveDuration, optionTweenDuration, optionStaggerDelay, inactiveOptionScale, inactiveOptionAlpha);

            QuestionResponseExampleView responseView = createResponseView
                ? CreateResponseView(targetCanvas.transform)
                : null;

            if (createRuntimeConnector)
            {
                CreateRuntimeConnector(targetCanvas.transform, menuView, responseView);
            }

            Undo.RegisterCreatedObjectUndo(rootObject, "Create Runtime Branch Menu UI");
            Selection.activeGameObject = rootObject;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void EnsureEventSystem()
        {
            EventSystem existingEventSystem = FindFirstObjectByType<EventSystem>();
            if (existingEventSystem != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
        }

        private QuestionMenuRuntimeConnector CreateRuntimeConnector(
            Transform parent,
            QuestionBranchMenuRuntimeView menuView,
            QuestionResponseExampleView responseView)
        {
            GameObject connectorObject = new GameObject("Question Menu Runtime Connector");
            connectorObject.transform.SetParent(parent, false);

            QuestionMenuRuntimeConnector connector = connectorObject.AddComponent<QuestionMenuRuntimeConnector>();
            CharacterInteractionController[] characterInteractionControllers = FindObjectsByType<CharacterInteractionController>(FindObjectsSortMode.None);
            connector.ConfigureForEditor(caseData, characterInteractionControllers, menuView, responseView);
            Undo.RegisterCreatedObjectUndo(connectorObject, "Create Question Menu Runtime Connector");
            return connector;
        }

        private QuestionResponseExampleView CreateResponseView(Transform parent)
        {
            GameObject responseObject = CreateUiObject("Question Response Example View", parent);
            RectTransform responseRectTransform = responseObject.transform as RectTransform;
            StretchToParent(responseRectTransform);

            CanvasGroup responseCanvasGroup = responseObject.AddComponent<CanvasGroup>();

            Image overlayImage = responseObject.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.62f);

            GameObject panelObject = CreateCardObject("Response Panel", responseObject.transform, new Color(0.10f, 0.10f, 0.12f, 0.98f));
            RectTransform panelRectTransform = panelObject.transform as RectTransform;
            ConfigureResponsePanel(panelRectTransform);

            TMP_Text speakerNameText = CreateText("Speaker Name", panelObject.transform, "Speaker", 24, FontStyles.Bold);
            RectTransform speakerRectTransform = speakerNameText.transform as RectTransform;
            ConfigurePanelText(speakerRectTransform, -24f, 34f);

            TMP_Text responseText = CreateText("Response Text", panelObject.transform, "Response text will appear here.", 18, FontStyles.Normal);
            responseText.alignment = TextAlignmentOptions.TopLeft;
            RectTransform responseTextRectTransform = responseText.transform as RectTransform;
            ConfigurePanelText(responseTextRectTransform, -72f, 180f);

            Button closeButton = CreateButton("Close Button", panelObject.transform, "Close");
            RectTransform closeButtonRectTransform = closeButton.transform as RectTransform;
            ConfigurePanelButton(closeButtonRectTransform);

            QuestionResponseExampleView responseView = responseObject.AddComponent<QuestionResponseExampleView>();
            responseView.ConfigureForEditor(responseCanvasGroup, speakerNameText, responseText, closeButton);
            responseObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(responseObject, "Create Question Response View");
            return responseView;
        }

        private Button CreateButton(string objectName, Transform parent, string label)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.26f, 0.26f, 0.30f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            TMP_Text buttonLabelText = CreateText("Label", buttonObject.transform, label, 16, FontStyles.Bold);
            buttonLabelText.alignment = TextAlignmentOptions.Center;
            RectTransform labelRectTransform = buttonLabelText.transform as RectTransform;
            StretchToParent(labelRectTransform);
            return button;
        }

        private TMP_Text CreateText(
            string objectName,
            Transform parent,
            string text,
            int fontSize,
            FontStyles fontStyle)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TMP_Text tmpText = textObject.AddComponent<TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = fontSize;
            tmpText.fontStyle = fontStyle;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.MidlineLeft;
            tmpText.enableWordWrapping = true;
            return tmpText;
        }

        private GameObject CreateCardObject(string objectName, Transform parent, Color color)
        {
            GameObject cardObject = CreateUiObject(objectName, parent);
            Image cardImage = cardObject.AddComponent<Image>();
            cardImage.color = color;
            return cardObject;
        }

        private GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
            uiObject.layer = LayerMask.NameToLayer("UI");
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private void ConfigureCenteredPanel(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(DefaultPanelWidth, DefaultPanelHeight);
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private void ConfigureHeaderText(RectTransform rectTransform, float topOffset, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(24f, 0f);
            rectTransform.offsetMax = new Vector2(-24f, 0f);
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, height);
            rectTransform.anchoredPosition = new Vector2(0f, topOffset);
        }

        private void ConfigureColumnsViewport(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(24f, 24f);
            rectTransform.offsetMax = new Vector2(-24f, -112f);
        }

        private void ConfigureColumnsRoot(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private void ConfigureResponsePanel(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(760f, 360f);
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private void ConfigurePanelText(RectTransform rectTransform, float topOffset, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(28f, 0f);
            rectTransform.offsetMax = new Vector2(-28f, 0f);
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, height);
            rectTransform.anchoredPosition = new Vector2(0f, topOffset);
        }

        private void ConfigurePanelButton(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.offsetMin = new Vector2(28f, 24f);
            rectTransform.offsetMax = new Vector2(-28f, 24f);
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, 44f);
        }

        private void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
