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
    public static class NoteCardPrefabCreator
    {
        private const string PrefabFolder = "Assets/ManWellNotes/Prefabs";
        private const string PrefabPath = PrefabFolder + "/Note Card.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FontPath = "Assets/ManWellNotes/Fonts/Generated/Sekaiwo Notes.asset";
        private const string PinnedIconPath =
            "Assets/ManWellNotes/Icons/General Icons/Set 1/1024 px/Black/Untitled-1_0000s_0003_Layer-32.png";

        [MenuItem("Tools/Man-Well Notes/Create and Assign Note Card Prefab")]
        public static void CreateAndAssign()
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingPrefab != null)
            {
                AssignPrefabToScene(existingPrefab);
                Selection.activeObject = existingPrefab;
                Debug.Log("Assigned the existing Note Card prefab without overwriting manual prefab edits.");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new System.InvalidOperationException("The generated notes font could not be loaded.");

            Sprite pinnedSprite = AssetDatabase.LoadAllAssetsAtPath(PinnedIconPath)
                .OfType<Sprite>()
                .FirstOrDefault();
            if (pinnedSprite == null)
                throw new System.InvalidOperationException("The starred-note icon could not be loaded.");

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/ManWellNotes", "Prefabs");

            GameObject root = new GameObject(
                "Note Card",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement),
                typeof(Button),
                typeof(LongPressButton),
                typeof(NoteCardView));

            Image background = root.GetComponent<Image>();
            background.color = new Color32(239, 239, 239, 255);

            LayoutElement size = root.GetComponent<LayoutElement>();
            size.preferredHeight = 152f;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;

            Image pinnedIcon = CreatePinnedIcon(root.transform, pinnedSprite);
            TMP_Text title = CreateText(
                "Title",
                "Untitled note",
                root.transform,
                font,
                20f,
                FontStyles.Bold,
                new Color32(12, 12, 12, 255));
            SetCardTextRect(title, 48f, 16f, 12f, 30f);
            title.enableAutoSizing = true;
            title.fontSizeMax = 20f;
            title.fontSizeMin = 16f;

            TMP_Text preview = CreateText(
                "Preview",
                "No additional text",
                root.transform,
                font,
                17f,
                FontStyles.Normal,
                new Color32(103, 103, 103, 255));
            SetCardTextRect(preview, 16f, 16f, 48f, 25f);

            TMP_Text created = CreateText(
                "Created timestamp",
                "Created Aug 18, 2026 · 12:00 PM",
                root.transform,
                font,
                15f,
                FontStyles.Normal,
                new Color32(103, 103, 103, 255));
            SetCardTextRect(created, 16f, 16f, 83f, 24f);

            NoteCardView view = root.GetComponent<NoteCardView>();
            SerializedObject viewData = new SerializedObject(view);
            viewData.FindProperty("background").objectReferenceValue = background;
            viewData.FindProperty("openButton").objectReferenceValue = button;
            viewData.FindProperty("longPressButton").objectReferenceValue = root.GetComponent<LongPressButton>();
            viewData.FindProperty("pinnedIcon").objectReferenceValue = pinnedIcon;
            viewData.FindProperty("titleText").objectReferenceValue = title;
            viewData.FindProperty("previewText").objectReferenceValue = preview;
            viewData.FindProperty("createdText").objectReferenceValue = created;
            viewData.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (prefab == null)
                throw new System.InvalidOperationException("The note-card prefab could not be saved.");

            AssignPrefabToScene(prefab);
            Selection.activeObject = prefab;
            Debug.Log("Created and assigned the editor-visible Note Card prefab.");
        }

        private static void AssignPrefabToScene(GameObject prefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            NotesApp app = Object.FindFirstObjectByType<NotesApp>(FindObjectsInactive.Include);
            if (app == null)
                throw new System.InvalidOperationException("NotesApp was not found in SampleScene.");

            SerializedObject appData = new SerializedObject(app);
            appData.FindProperty("noteCardPrefab").objectReferenceValue = prefab.GetComponent<NoteCardView>();
            appData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Image CreatePinnedIcon(Transform parent, Sprite sprite)
        {
            GameObject iconObject = new GameObject(
                "Starred icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconObject.transform.SetParent(parent, false);

            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            RectTransform rect = icon.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -15f);
            rect.sizeDelta = new Vector2(22f, 22f);
            return icon;
        }

        private static TMP_Text CreateText(
            string objectName,
            string value,
            Transform parent,
            TMP_FontAsset font,
            float size,
            FontStyles style,
            Color color)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void SetCardTextRect(
            TMP_Text text,
            float left,
            float right,
            float top,
            float height)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
