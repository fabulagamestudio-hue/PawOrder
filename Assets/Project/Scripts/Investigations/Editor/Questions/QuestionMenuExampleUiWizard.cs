using Fabula.PawOrder;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fabula.PawOrder.Editor
{
    public static class QuestionMenuExampleUiWizard
    {
        #region Constants

        private const string MenuPath = "Fabula/Paw Order/Create Question Menu Example UI";
        private const string RootName = "Question Menu Example UI";
        private const string TemplatesName = "Templates";
        private const float PanelWidth = 720f;
        private const float PanelHeight = 760f;
        private const float Padding = 24f;
        private const float Spacing = 10f;

        #endregion

        #region Public API

        [MenuItem(MenuPath)]
        public static void CreateQuestionMenuExampleUi()
        {
            Canvas canvas = CreateCanvas();
            EnsureEventSystem();

            GameObject rootObject = CreateUiObject(RootName, canvas.transform);
            RectTransform rootRectTransform = rootObject.transform as RectTransform;
            ConfigureCenteredPanel(rootRectTransform);

            CanvasGroup rootCanvasGroup = rootObject.AddComponent<CanvasGroup>();
            Image backgroundImage = rootObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.08f, 0.09f, 0.96f);

            VerticalLayoutGroup rootLayout = rootObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
            rootLayout.spacing = Spacing;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            QuestionMenuExampleView rootView = rootObject.AddComponent<QuestionMenuExampleView>();

            TMP_Text titleText = CreateText("Title", rootObject.transform, "Question Menu Example", 28, FontStyles.Bold);
            AddLayoutElement(titleText.gameObject, -1f, 42f);

            TMP_Text subtitleText = CreateText("Subtitle", rootObject.transform, "Current NPC: none", 18, FontStyles.Normal);
            AddLayoutElement(subtitleText.gameObject, -1f, 30f);

            ScrollRect groupsScrollRect = CreateScrollRect("Groups Scroll View", rootObject.transform);
            AddLayoutElement(groupsScrollRect.gameObject, -1f, 1f, true);

            RectTransform groupsContentRoot = groupsScrollRect.content;
            GameObject templatesRoot = CreateUiObject(TemplatesName, rootObject.transform);
            templatesRoot.SetActive(false);

            QuestionMenuEntryExampleView entryTemplate = CreateEntryTemplate(templatesRoot.transform);
            QuestionMenuTargetExampleView targetTemplate = CreateTargetTemplate(templatesRoot.transform, entryTemplate);
            QuestionMenuGroupExampleView groupTemplate = CreateGroupTemplate(templatesRoot.transform, targetTemplate);

            rootView.ConfigureForEditor(
                rootCanvasGroup,
                titleText,
                subtitleText,
                groupsContentRoot,
                groupTemplate);

            QuestionResponseExampleView responseView = CreateResponseView(canvas.transform);
            CreateRuntimeConnector(canvas.transform, rootView, responseView);

            Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Create Question Menu Example UI");
            Selection.activeGameObject = rootObject;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        #endregion

        #region Internal Logic

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = CreateRootObject("Question Menu Example Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existingEventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (existingEventSystem != null)
            {
                return;
            }

            GameObject eventSystemObject = CreateRootObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static QuestionMenuGroupExampleView CreateGroupTemplate(
            Transform parent,
            QuestionMenuTargetExampleView targetTemplate)
        {
            GameObject groupObject = CreateCardObject("Group Template", parent, new Color(0.14f, 0.14f, 0.16f, 1f));
            VerticalLayoutGroup layout = groupObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = groupObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button toggleButton = groupObject.AddComponent<Button>();
            toggleButton.targetGraphic = groupObject.GetComponent<Image>();

            TMP_Text labelText = CreateText("Group Label", groupObject.transform, "▶ Group", 22, FontStyles.Bold);
            AddLayoutElement(labelText.gameObject, -1f, 34f);

            GameObject targetsRootObject = CreateUiObject("Targets", groupObject.transform);
            RectTransform targetsRoot = targetsRootObject.transform as RectTransform;
            CanvasGroup targetsCanvasGroup = targetsRootObject.AddComponent<CanvasGroup>();
            VerticalLayoutGroup targetsLayout = targetsRootObject.AddComponent<VerticalLayoutGroup>();
            targetsLayout.spacing = 8f;
            targetsLayout.childControlWidth = true;
            targetsLayout.childControlHeight = true;
            targetsLayout.childForceExpandWidth = true;
            targetsLayout.childForceExpandHeight = false;

            ContentSizeFitter targetsFitter = targetsRootObject.AddComponent<ContentSizeFitter>();
            targetsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            targetsRootObject.SetActive(false);
            targetsCanvasGroup.alpha = 0f;

            QuestionMenuGroupExampleView groupView = groupObject.AddComponent<QuestionMenuGroupExampleView>();
            groupView.ConfigureForEditor(toggleButton, labelText, targetsRoot, targetsCanvasGroup, targetTemplate);
            return groupView;
        }

        private static QuestionMenuTargetExampleView CreateTargetTemplate(
            Transform parent,
            QuestionMenuEntryExampleView entryTemplate)
        {
            GameObject targetObject = CreateCardObject("Target Template", parent, new Color(0.18f, 0.18f, 0.20f, 1f));
            VerticalLayoutGroup layout = targetObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = targetObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button toggleButton = targetObject.AddComponent<Button>();
            toggleButton.targetGraphic = targetObject.GetComponent<Image>();

            TMP_Text labelText = CreateText("Target Label", targetObject.transform, "▶ Target", 18, FontStyles.Bold);
            AddLayoutElement(labelText.gameObject, -1f, 28f);

            GameObject entriesRootObject = CreateUiObject("Entries", targetObject.transform);
            RectTransform entriesRoot = entriesRootObject.transform as RectTransform;
            CanvasGroup entriesCanvasGroup = entriesRootObject.AddComponent<CanvasGroup>();
            VerticalLayoutGroup entriesLayout = entriesRootObject.AddComponent<VerticalLayoutGroup>();
            entriesLayout.spacing = 5f;
            entriesLayout.childControlWidth = true;
            entriesLayout.childControlHeight = true;
            entriesLayout.childForceExpandWidth = true;
            entriesLayout.childForceExpandHeight = false;

            ContentSizeFitter entriesFitter = entriesRootObject.AddComponent<ContentSizeFitter>();
            entriesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            entriesRootObject.SetActive(false);
            entriesCanvasGroup.alpha = 0f;

            QuestionMenuTargetExampleView targetView = targetObject.AddComponent<QuestionMenuTargetExampleView>();
            targetView.ConfigureForEditor(toggleButton, labelText, entriesRoot, entriesCanvasGroup, entryTemplate);
            return targetView;
        }

        private static QuestionMenuEntryExampleView CreateEntryTemplate(Transform parent)
        {
            GameObject entryObject = CreateUiObject("Entry Template", parent);
            Image backgroundImage = entryObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.24f, 0.24f, 0.27f, 1f);

            Button button = entryObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;

            HorizontalLayoutGroup layout = entryObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            AddLayoutElement(entryObject, -1f, 44f);

            TMP_Text questionLabelText = CreateText("Question Label", entryObject.transform, "Question label", 16, FontStyles.Normal);
            questionLabelText.enableWordWrapping = true;
            AddLayoutElement(questionLabelText.gameObject, 1f, 28f, true);

            TMP_Text stateLabelText = CreateText("State Label", entryObject.transform, "Locked", 13, FontStyles.Italic);
            stateLabelText.alignment = TextAlignmentOptions.MidlineRight;
            AddLayoutElement(stateLabelText.gameObject, 80f, 28f);

            TMP_Text newBadgeText = CreateText("New Badge", entryObject.transform, "NEW", 13, FontStyles.Bold);
            newBadgeText.alignment = TextAlignmentOptions.MidlineRight;
            AddLayoutElement(newBadgeText.gameObject, 48f, 28f);

            QuestionMenuEntryExampleView entryView = entryObject.AddComponent<QuestionMenuEntryExampleView>();
            entryView.ConfigureForEditor(button, questionLabelText, stateLabelText, newBadgeText.gameObject);
            return entryView;
        }


        private static QuestionResponseExampleView CreateResponseView(Transform parent)
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

            VerticalLayoutGroup panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 24, 24);
            panelLayout.spacing = 14f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            TMP_Text speakerNameText = CreateText("Speaker Name", panelObject.transform, "Speaker", 24, FontStyles.Bold);
            AddLayoutElement(speakerNameText.gameObject, -1f, 34f);

            TMP_Text responseText = CreateText("Response Text", panelObject.transform, "Response text will appear here.", 18, FontStyles.Normal);
            responseText.alignment = TextAlignmentOptions.TopLeft;
            responseText.enableWordWrapping = true;
            AddLayoutElement(responseText.gameObject, -1f, 180f, true);

            Button closeButton = CreateButton("Close Button", panelObject.transform, "Close");
            AddLayoutElement(closeButton.gameObject, -1f, 44f);

            QuestionResponseExampleView responseView = responseObject.AddComponent<QuestionResponseExampleView>();
            responseView.ConfigureForEditor(responseCanvasGroup, speakerNameText, responseText, closeButton);
            responseObject.SetActive(false);
            return responseView;
        }

        private static QuestionMenuRuntimeConnector CreateRuntimeConnector(
            Transform parent,
            QuestionMenuExampleView menuView,
            QuestionResponseExampleView responseView)
        {
            GameObject connectorObject = CreateRootObject("Question Menu Runtime Connector");
            connectorObject.transform.SetParent(parent, false);

            QuestionMenuRuntimeConnector connector = connectorObject.AddComponent<QuestionMenuRuntimeConnector>();
            CharacterInteractionController[] characterInteractionControllers = Object.FindObjectsByType<CharacterInteractionController>(FindObjectsSortMode.None);
            connector.ConfigureForEditor(null, characterInteractionControllers, menuView, responseView);
            return connector;
        }

        private static Button CreateButton(string objectName, Transform parent, string label)
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

        private static ScrollRect CreateScrollRect(string objectName, Transform parent)
        {
            GameObject scrollRootObject = CreateUiObject(objectName, parent);
            Image scrollBackground = scrollRootObject.AddComponent<Image>();
            scrollBackground.color = new Color(0.11f, 0.11f, 0.12f, 1f);

            ScrollRect scrollRect = scrollRootObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportObject = CreateUiObject("Viewport", scrollRootObject.transform);
            RectTransform viewportRectTransform = viewportObject.transform as RectTransform;
            StretchToParent(viewportRectTransform);

            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            Mask viewportMask = viewportObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            GameObject contentObject = CreateUiObject("Content", viewportObject.transform);
            RectTransform contentRectTransform = contentObject.transform as RectTransform;
            contentRectTransform.anchorMin = new Vector2(0f, 1f);
            contentRectTransform.anchorMax = new Vector2(1f, 1f);
            contentRectTransform.pivot = new Vector2(0.5f, 1f);
            contentRectTransform.offsetMin = new Vector2(12f, contentRectTransform.offsetMin.y);
            contentRectTransform.offsetMax = new Vector2(-12f, contentRectTransform.offsetMax.y);

            VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(12, 12, 12, 12);
            contentLayout.spacing = 12f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRectTransform;
            scrollRect.content = contentRectTransform;
            return scrollRect;
        }

        private static TMP_Text CreateText(
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

        private static GameObject CreateCardObject(string objectName, Transform parent, Color color)
        {
            GameObject cardObject = CreateUiObject(objectName, parent);
            Image cardImage = cardObject.AddComponent<Image>();
            cardImage.color = color;
            return cardObject;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
            uiObject.layer = LayerMask.NameToLayer("UI");
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static GameObject CreateRootObject(string objectName)
        {
            GameObject rootObject = new GameObject(objectName);
            rootObject.layer = LayerMask.NameToLayer("UI");
            return rootObject;
        }

        private static LayoutElement AddLayoutElement(
            GameObject targetObject,
            float preferredWidth,
            float preferredHeight,
            bool flexibleWidth = false)
        {
            LayoutElement layoutElement = targetObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f)
            {
                layoutElement.preferredWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                layoutElement.preferredHeight = preferredHeight;
            }

            layoutElement.flexibleWidth = flexibleWidth ? 1f : 0f;
            return layoutElement;
        }

        private static void ConfigureCenteredPanel(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rectTransform.anchoredPosition = Vector2.zero;
        }


        private static void ConfigureResponsePanel(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(760f, 360f);
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void StretchToParent(RectTransform rectTransform)
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
