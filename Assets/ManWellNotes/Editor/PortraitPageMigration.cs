using System.Linq;
using ManWellNotes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ManWellNotesEditor
{
    // Converts the existing scene hierarchy in place. Unlike NotesSceneBuilder,
    // this tool never deletes or rebuilds the user's page objects.
    public static class PortraitPageMigration
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Man-Well Notes/Apply Portrait Page Layout")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject root = RequireObject("Man-Well Notes");
            GameObject appShell = RequireObject("App");
            GameObject libraryPage = RequireObject("Sidebar");
            GameObject notesPage = RequireObject("Notes list");
            GameObject editorPage = RequireObject("Editor");
            GameObject editorToolbar = RequireObject("Toolbar");
            NotesApp notesApp = root.GetComponent<NotesApp>();

            if (notesApp == null)
                throw new System.InvalidOperationException("The scene root has no NotesApp component.");

            // Preserve the component and its values for reference, but stop it from
            // arranging all three pages side by side.
            HorizontalLayoutGroup oldDesktopLayout = appShell.GetComponent<HorizontalLayoutGroup>();
            if (oldDesktopLayout != null)
                oldDesktopLayout.enabled = false;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(360f, 640f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            PreparePage(libraryPage);
            PreparePage(notesPage);
            PreparePage(editorPage);

            TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true)
                .Select(text => text.font)
                .FirstOrDefault(candidate => candidate != null);

            if (font == null)
                throw new System.InvalidOperationException("No assigned TMP font was found in the existing scene.");

            TMP_Text createdText = GetOrCreateCreatedTimestamp(notesApp, editorPage.transform, font);

            Button notesBack = GetOrCreateBackButton(
                notesPage.transform,
                "Back to library",
                "‹  Notes",
                font,
                Color.black,
                Color.white,
                true);

            Button editorBack = GetOrCreateBackButton(
                editorToolbar.transform,
                "Back to notes",
                "‹  Back",
                font,
                Color.black,
                Color.white,
                false);

            NotesPageNavigator navigator = root.GetComponent<NotesPageNavigator>();
            if (navigator == null)
                navigator = root.AddComponent<NotesPageNavigator>();

            SerializedObject navigatorData = new SerializedObject(navigator);
            navigatorData.FindProperty("libraryPage").objectReferenceValue = libraryPage;
            navigatorData.FindProperty("notesPage").objectReferenceValue = notesPage;
            navigatorData.FindProperty("editorPage").objectReferenceValue = editorPage;
            navigatorData.FindProperty("notesBackButton").objectReferenceValue = notesBack;
            navigatorData.FindProperty("editorBackButton").objectReferenceValue = editorBack;
            navigatorData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject appData = new SerializedObject(notesApp);
            appData.FindProperty("pageNavigator").objectReferenceValue = navigator;
            appData.FindProperty("createdText").objectReferenceValue = createdText;
            appData.ApplyModifiedPropertiesWithoutUndo();

            ApplyAccessibleTypography(root);

            // Save the scene showing its first page, exactly as it will open.
            libraryPage.SetActive(true);
            notesPage.SetActive(false);
            editorPage.SetActive(false);

            ConfigurePortraitOrientation();

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(navigator);
            EditorUtility.SetDirty(notesApp);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;

            Debug.Log("Applied the portrait, three-page layout without rebuilding existing page objects.");
        }

        private static void ApplyAccessibleTypography(GameObject root)
        {
            NotesApp app = root.GetComponent<NotesApp>();
            SerializedObject appData = new SerializedObject(app);

            SetInputFontSize(appData.FindProperty("searchField").objectReferenceValue as TMP_InputField, 18f);
            SetInputFontSize(appData.FindProperty("titleField").objectReferenceValue as TMP_InputField, 34f);
            SetInputFontSize(appData.FindProperty("bodyField").objectReferenceValue as TMP_InputField, 20f);
            SetTextFontSize(appData.FindProperty("createdText").objectReferenceValue as TMP_Text, 16f);
            SetTextFontSize(appData.FindProperty("notesHeading").objectReferenceValue as TMP_Text, 30f);
            SetTextFontSize(appData.FindProperty("emptyMessage").objectReferenceValue as TMP_Text, 18f);
            SetTextFontSize(appData.FindProperty("statusText").objectReferenceValue as TMP_Text, 16f);

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string parentName = text.transform.parent != null ? text.transform.parent.name : string.Empty;

                switch (parentName)
                {
                    case "New note":
                        SetTextFontSize(text, 19f);
                        break;
                    case "All notes":
                    case "Pinned":
                        SetTextFontSize(text, 18f);
                        break;
                    case "Pin":
                    case "Delete":
                        SetTextFontSize(text, 17f);
                        break;
                    case "Back to library":
                    case "Back to notes":
                        SetTextFontSize(text, 22f);
                        break;
                }

                switch (text.text)
                {
                    case "MAN-WELL":
                        SetTextFontSize(text, 28f);
                        break;
                    case "NOTES":
                    case "LIBRARY":
                    case "Stored privately on this device":
                        SetTextFontSize(text, 14f);
                        break;
                }
            }
        }

        private static TMP_Text GetOrCreateCreatedTimestamp(
            NotesApp app,
            Transform editor,
            TMP_FontAsset font)
        {
            Transform existing = editor.Find("Created timestamp");
            TMP_Text text;

            if (existing != null)
            {
                text = existing.GetComponent<TMP_Text>();
            }
            else
            {
                GameObject textObject = new GameObject(
                    "Created timestamp",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI),
                    typeof(LayoutElement));
                textObject.transform.SetParent(editor, false);
                text = textObject.GetComponent<TMP_Text>();
            }

            text.text = "Created date and time";
            text.font = font;
            text.fontSize = 16f;
            text.fontStyle = FontStyles.Normal;
            text.color = new Color32(103, 103, 103, 255);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            text.GetComponent<LayoutElement>().preferredHeight = 24f;

            SerializedObject appData = new SerializedObject(app);
            TMP_InputField titleField =
                appData.FindProperty("titleField").objectReferenceValue as TMP_InputField;
            if (titleField != null)
                text.transform.SetSiblingIndex(titleField.transform.GetSiblingIndex() + 1);

            TMP_InputField bodyField =
                appData.FindProperty("bodyField").objectReferenceValue as TMP_InputField;
            if (bodyField != null)
            {
                LayoutElement bodySize = bodyField.GetComponent<LayoutElement>();
                if (bodySize != null)
                    bodySize.minHeight = 300f;
            }

            EditorUtility.SetDirty(text);
            return text;
        }

        private static void SetInputFontSize(TMP_InputField input, float size)
        {
            if (input == null)
                return;

            SetTextFontSize(input.textComponent, size);
            SetTextFontSize(input.placeholder as TMP_Text, size);
            EditorUtility.SetDirty(input);
        }

        private static void SetTextFontSize(TMP_Text text, float size)
        {
            if (text == null)
                return;

            text.enableAutoSizing = false;
            text.fontSize = size;
            EditorUtility.SetDirty(text);
        }

        private static void PreparePage(GameObject page)
        {
            RectTransform rect = page.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            // Width constraints only belonged to the former desktop layout.
            LayoutElement widthConstraint = page.GetComponent<LayoutElement>();
            if (widthConstraint != null)
                widthConstraint.enabled = false;

            EditorUtility.SetDirty(rect);
        }

        private static Button GetOrCreateBackButton(
            Transform parent,
            string objectName,
            string label,
            TMP_FontAsset font,
            Color background,
            Color foreground,
            bool verticalLayout)
        {
            Transform existing = parent.Find(objectName);
            GameObject buttonObject;

            if (existing != null)
            {
                buttonObject = existing.gameObject;
            }
            else
            {
                buttonObject = new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button),
                    typeof(LayoutElement));
                buttonObject.transform.SetParent(parent, false);

                GameObject textObject = new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                textObject.transform.SetParent(buttonObject.transform, false);

                RectTransform textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(18f, 0f);
                textRect.offsetMax = new Vector2(-18f, 0f);
            }

            Image image = buttonObject.GetComponent<Image>();
            image.color = background;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            text.font = font;
            text.fontSize = verticalLayout ? 28f : 22f;
            text.fontStyle = FontStyles.Bold;
            text.color = foreground;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            LayoutElement size = buttonObject.GetComponent<LayoutElement>();
            if (verticalLayout)
                size.preferredHeight = 64f;
            else
                size.preferredWidth = 120f;

            buttonObject.transform.SetSiblingIndex(0);
            EditorUtility.SetDirty(buttonObject);
            return button;
        }

        private static GameObject RequireObject(string name)
        {
            GameObject result = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(candidate =>
                    candidate.scene.IsValid() &&
                    candidate.scene.path == ScenePath &&
                    candidate.name == name);

            if (result == null)
                throw new System.InvalidOperationException($"Required scene object was not found: {name}");
            return result;
        }

        private static void ConfigurePortraitOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }
    }
}
