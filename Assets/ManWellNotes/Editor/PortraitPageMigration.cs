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
            scaler.referenceResolution = new Vector2(1080f, 1920f);
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
            appData.ApplyModifiedPropertiesWithoutUndo();

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
