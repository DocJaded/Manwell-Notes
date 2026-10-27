using System;
using System.IO;
using System.Linq;
using ManWellNotes;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ManWellNotesEditor
{
    public static class NotePhotoSetup
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SectionPath = "Assets/ManWellNotes/Prefabs/Note Picture.prefab";
        private const string DialogsPath = "Assets/ManWellNotes/Prefabs/Picture Dialogs.prefab";
        private const string PluginPath = PhotoSpike.PhotoSpikeSetup.PluginPath;

        [MenuItem("Tools/Man-Well Notes/Photos/Add Missing Photo Interface")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            NotesApp app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NotesApp>(true)).Single();
            var appData = new SerializedObject(app);
            Transform editor = ((GameObject)appData.FindProperty("editorPanel").objectReferenceValue).transform;
            TMP_FontAsset font = (TMP_FontAsset)appData.FindProperty("font").objectReferenceValue;
            var controller = app.GetComponent<NotePhotoController>();
            if (controller == null) controller = Undo.AddComponent<NotePhotoController>(app.gameObject);
            var existing = new SerializedObject(controller);
            EnsurePrefab(SectionPath, () => BuildSection(font));
            EnsurePrefab(DialogsPath, () => BuildDialogs(font));
            AddMissingViewerZoom();
            var savedSection = existing.FindProperty("section").objectReferenceValue as GameObject;
            Transform section = savedSection != null ? savedSection.transform : editor.Find("Note Picture");
            if (section == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SectionPath), editor);
                Undo.RegisterCreatedObjectUndo(instance, "Add note picture controls");
                section = instance.transform;
                var body = (TMP_InputField)appData.FindProperty("bodyField").objectReferenceValue;
                section.SetSiblingIndex(body.transform.GetSiblingIndex() + 1);
                // One-time saved layout migration: the text area can shrink to fit the
                // picture without squeezing the title or bottom navigation. Subsequent
                // setup runs preserve the owner's Inspector adjustments.
                var bodySize = body.GetComponent<LayoutElement>();
                Undo.RecordObject(bodySize, "Make room for note picture");
                bodySize.minHeight = 100;
                var title = (TMP_InputField)appData.FindProperty("titleField").objectReferenceValue;
                var titleSize = title.GetComponent<LayoutElement>();
                Undo.RecordObject(titleSize, "Preserve note title height");
                titleSize.minHeight = titleSize.preferredHeight;
                var toolbarSize = editor.Find("Toolbar").GetComponent<LayoutElement>();
                Undo.RecordObject(toolbarSize, "Preserve note navigation height");
                toolbarSize.minHeight = toolbarSize.preferredHeight;
            }
            var savedSource = existing.FindProperty("sourceDialog").objectReferenceValue as GameObject;
            Transform dialogs = savedSource != null ? savedSource.transform.parent : app.transform.Find("Picture Dialogs");
            if (dialogs == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DialogsPath), app.transform);
                Undo.RegisterCreatedObjectUndo(instance, "Add picture dialogs");
                dialogs = instance.transform;
                Stretch((RectTransform)dialogs);
                PrefabUtility.RecordPrefabInstancePropertyModifications(dialogs);
            }
            var data = new SerializedObject(controller);
            Assign(data, "app", app);
            Assign(data, "section", section.gameObject);
            Assign(data, "previewFrame", section.Find("Preview").gameObject);
            Assign(data, "preview", section.Find("Preview/Viewport/Image").GetComponent<RawImage>());
            Assign(data, "previewAspect", section.Find("Preview/Viewport/Image").GetComponent<AspectRatioFitter>());
            Assign(data, "missingImage", section.Find("Preview/Missing picture").gameObject);
            Assign(data, "addButton", section.Find("Actions/Add picture").GetComponent<Button>());
            Assign(data, "removeButton", section.Find("Actions/Remove picture").GetComponent<Button>());
            Assign(data, "viewButton", section.Find("Preview").GetComponent<Button>());
            Assign(data, "pendingPanel", section.Find("Pending request").gameObject);
            Assign(data, "retryButton", section.Find("Pending request/Actions/Retry save").gameObject);
            Assign(data, "sourceDialog", dialogs.Find("Source").gameObject);
            Assign(data, "replacementDialog", dialogs.Find("Replacement").gameObject);
            Assign(data, "replacementNo", dialogs.Find("Replacement/Panel/Buttons/No").GetComponent<Button>());
            Assign(data, "removeDialog", dialogs.Find("Remove").gameObject);
            Assign(data, "viewerDialog", dialogs.Find("Viewer").gameObject);
            Assign(data, "fullImage", dialogs.Find("Viewer/Viewport/Image").GetComponent<RawImage>());
            Assign(data, "fullAspect", dialogs.Find("Viewer/Viewport/Image").GetComponent<AspectRatioFitter>());
            Assign(data, "messageDialog", dialogs.Find("Message").gameObject);
            Assign(data, "messageText", dialogs.Find("Message/Panel/Text").GetComponent<TMP_Text>());
            data.ApplyModifiedProperties();
            Wire(section, "Actions/Add picture", controller.OpenSource);
            Wire(section, "Actions/Remove picture", controller.AskRemove);
            Wire(section, "Preview", controller.OpenViewer);
            Wire(section, "Pending request/Actions/Retry save", controller.RetrySave);
            Wire(section, "Pending request/Actions/Cancel request", controller.CancelRequest);
            Wire(dialogs, "Source/Panel/Buttons/Take photo", controller.TakePhoto);
            Wire(dialogs, "Source/Panel/Buttons/Choose image", controller.ChooseImage);
            Wire(dialogs, "Source/Panel/Buttons/Cancel", controller.CloseDialogs);
            Wire(dialogs, "Replacement/Panel/Buttons/Yes", controller.ConfirmReplacement);
            Wire(dialogs, "Replacement/Panel/Buttons/No", controller.CloseDialogs);
            Wire(dialogs, "Remove/Panel/Buttons/Yes", controller.ConfirmRemove);
            Wire(dialogs, "Remove/Panel/Buttons/No", controller.CloseDialogs);
            Wire(dialogs, "Message/Panel/Buttons/OK", controller.CloseDialogs);
            Wire(dialogs, "Viewer/Close", controller.CloseDialogs);
            Assign(appData, "photoController", controller);
            appData.ApplyModifiedProperties();
            var navigator = new SerializedObject(app.GetComponent<NotesPageNavigator>());
            Assign(navigator, "photoController", controller);
            navigator.ApplyModifiedProperties();
            var plugin = (PluginImporter)AssetImporter.GetAtPath(PluginPath);
            plugin.SetCompatibleWithAnyPlatform(false);
            plugin.SetCompatibleWithEditor(false);
            plugin.SetCompatibleWithPlatform(BuildTarget.Android, true);
            EditorUtility.SetDirty(plugin);
            AssetDatabase.WriteImportSettingsIfDirty(PluginPath);
            plugin.SaveAndReimport();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Added the saved note picture interface. Existing scene and prefab styling was preserved.");
        }

        private static void AddMissingViewerZoom()
        {
            var root = PrefabUtility.LoadPrefabContents(DialogsPath);
            try
            {
                var viewport = root.transform.Find("Viewer/Viewport").GetComponent<RectTransform>();
                if (viewport.GetComponent<NotePhotoZoom>() != null) return;
                if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
                var zoom = viewport.gameObject.AddComponent<NotePhotoZoom>();
                var data = new SerializedObject(zoom);
                Assign(data, "viewport", viewport);
                Assign(data, "image", viewport.Find("Image").GetComponent<RectTransform>());
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DialogsPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static void Assign(SerializedObject data, string field, Object value)
        {
            // Preserve an owner's deliberate reference overrides on subsequent runs.
            if (data.FindProperty(field).objectReferenceValue == null) data.FindProperty(field).objectReferenceValue = value;
        }
        internal static void Wire(Transform root, string path, UnityAction action)
        {
            var button = root.Find(path).GetComponent<Button>();
            if (button.onClick.GetPersistentEventCount() > 0) return;
            Undo.RecordObject(button, "Wire picture button");
            UnityEventTools.AddPersistentListener(button.onClick, action);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }
        internal static void EnsurePrefab(string path, Func<GameObject> create)
        {
            if (File.Exists(path)) return;
            GameObject root = create();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static GameObject BuildSection(TMP_FontAsset font)
        {
            var root = Rect("Note Picture", null);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var actions = Row("Actions", root, 40);
            Button("Add picture", actions, font);
            Button("Remove picture", actions, font);
            var frame = Rect("Preview", root);
            frame.gameObject.AddComponent<Image>().color = new Color32(238, 238, 238, 255);
            frame.gameObject.AddComponent<Button>();
            var size = frame.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 96;
            var missing = Label("Missing picture", "Picture unavailable", frame, font, 17);
            Stretch(missing.rectTransform);
            var viewport = Rect("Viewport", frame);
            Stretch(viewport, 4, 4, 4, 4);
            Picture(viewport);
            frame.gameObject.SetActive(false);
            var pending = Rect("Pending request", root);
            var pendingLayout = pending.gameObject.AddComponent<VerticalLayoutGroup>();
            pendingLayout.childControlWidth = pendingLayout.childControlHeight = true;
            pendingLayout.childForceExpandWidth = true;
            pendingLayout.childForceExpandHeight = false;
            var label = Label("Status", "Preparing picture...", pending, font, 15);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            var row = Row("Actions", pending, 36);
            Button("Retry save", row, font);
            Button("Cancel request", row, font);
            pending.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static GameObject BuildDialogs(TMP_FontAsset font)
        {
            var root = Rect("Picture Dialogs", null);
            Stretch(root);
            Dialog(root, "Source", "Add a picture", new[] { "Take photo", "Choose image", "Cancel" }, font, true);
            Dialog(root, "Replacement", "The existing picture will be replaced", new[] { "Yes", "No" }, font);
            Dialog(root, "Remove", "Remove this picture from the note?", new[] { "Yes", "No" }, font);
            var viewer = Overlay("Viewer", root);
            var viewport = Rect("Viewport", viewer);
            Stretch(viewport, 16, 30, 16, 88);
            Picture(viewport);
            var close = Button("Close", viewer, font).GetComponent<RectTransform>();
            close.anchorMin = new Vector2(0, 0);
            close.anchorMax = new Vector2(1, 0);
            close.pivot = new Vector2(0.5f, 0);
            close.offsetMin = new Vector2(20, 28);
            close.offsetMax = new Vector2(-20, 72);
            viewer.gameObject.SetActive(false);
            Dialog(root, "Message", "Message", new[] { "OK" }, font, false, 240);
            return root.gameObject;
        }

        internal static void Dialog(Transform root, string name, string message, string[] labels, TMP_FontAsset font, bool vertical = false, float height = 200)
        {
            var overlay = Overlay(name, root);
            var panel = Rect("Panel", overlay);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(310, vertical ? 270 : height);
            panel.gameObject.AddComponent<Image>().color = Color.white;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var text = Label("Text", message, panel, font, 20);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14;
            text.fontSizeMax = 20;
            var textSize = text.gameObject.AddComponent<LayoutElement>();
            textSize.minHeight = 50;
            textSize.flexibleHeight = 1;
            RectTransform buttons;
            if (vertical)
            {
                buttons = Rect("Buttons", panel);
                var group = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
                group.spacing = 8;
                group.childControlWidth = group.childControlHeight = true;
                group.childForceExpandWidth = true;
                group.childForceExpandHeight = false;
                buttons.gameObject.AddComponent<LayoutElement>().preferredHeight = labels.Length * 44 + (labels.Length - 1) * 8;
            }
            else buttons = Row("Buttons", panel, 44);
            foreach (string label in labels)
            {
                Button button = Button(label, buttons, font);
                if (vertical) button.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            }
            overlay.gameObject.SetActive(false);
        }

        private static RectTransform Overlay(string name, Transform parent)
        {
            var rect = Rect(name, parent);
            Stretch(rect);
            rect.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            return rect;
        }
        private static void Picture(Transform parent)
        {
            var rect = Rect("Image", parent);
            rect.gameObject.AddComponent<RawImage>().raycastTarget = false;
            rect.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        }
        internal static RectTransform Row(string name, Transform parent, float height)
        {
            var row = Rect(name, parent);
            row.gameObject.AddComponent<LayoutElement>().minHeight = height;
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            return row;
        }
        internal static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }
        internal static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
        internal static TMP_Text Label(string name, string value, Transform parent, TMP_FontAsset font, float size)
        {
            var rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
        internal static Button Button(string name, Transform parent, TMP_FontAsset font)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color32(20, 20, 20, 255);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Label("Label", name, rect, font, 17);
            label.color = Color.white;
            Stretch(label.rectTransform, 6, 3, 6, 3);
            return button;
        }

        [MenuItem("Tools/Man-Well Notes/Photos/Validate Photo Interface")]
        public static void Validate()
        {
            var app = Resources.FindObjectsOfTypeAll<NotesApp>().Single(item => item.gameObject.scene.path == ScenePath);
            var controller = app.GetComponent<NotePhotoController>();
            if (controller == null) throw new BuildFailedException("Add the saved photo interface first.");
            var data = new SerializedObject(controller);
            foreach (string prefabPath in new[] { SectionPath, DialogsPath })
            {
                int instances = app.GetComponentsInChildren<Transform>(true).Count(item =>
                    PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject) &&
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject) == prefabPath);
                if (instances != 1) throw new BuildFailedException("Expected one saved photo prefab instance: " + prefabPath);
            }
            var iterator = data.GetIterator();
            while (iterator.NextVisible(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null)
                    throw new BuildFailedException("Missing photo reference: " + iterator.propertyPath);
            foreach (var rootName in new[] { "section", "sourceDialog", "replacementDialog", "removeDialog", "viewerDialog", "messageDialog" })
            {
                var root = (GameObject)data.FindProperty(rootName).objectReferenceValue;
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    if (button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != controller)
                        throw new BuildFailedException("Check saved photo button event: " + button.name);
            }
            Debug.Log("NOTE_PHOTO_UI_VALIDATED");
        }

        public static void VerifyAndBuild()
        {
            Apply();
            Apply(); // Setup is repeatable without duplicating controls or resetting prefabs.
            NotePhotoValidation.Run();
            RenderPreview();
            EditorSceneManager.OpenScene(ScenePath);
            BuildPreviewAndroid();
        }

        [MenuItem("Tools/Man-Well Notes/Photos/Build Notes With Photos APK")]
        public static void BuildAndroid() => BuildApk("Release_APK/ManWellNotes-Photos.apk", "com.manwell.notes");

        [MenuItem("Tools/Man-Well Notes/Build Updated Notes APK")]
        public static void BuildUpdatedApp()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            Validate();
            NoteAudioSetup.ValidateAudio();
            NotePhotoValidation.Run();
            NoteAudioValidation.Run();
            BuildApk("Release_APK/ManWellNotes-" + PlayerSettings.bundleVersion + ".apk", "com.manwell.notes", BuildOptions.None);
        }

        [MenuItem("Tools/Man-Well Notes/Photos/Build Separate Photos Preview APK")]
        public static void BuildPreviewAndroid()
        {
            string originalId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string originalName = PlayerSettings.productName;
            bool originalKeystore = PlayerSettings.Android.useCustomKeystore;
            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.manwell.notes.photospreview");
                PlayerSettings.productName = "Notes Photos Preview";
                PlayerSettings.Android.useCustomKeystore = false;
                BuildApk("Release_APK/NotesPhotosPreview.apk", "com.manwell.notes.photospreview");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalId);
                PlayerSettings.productName = originalName;
                PlayerSettings.Android.useCustomKeystore = originalKeystore;
                AssetDatabase.SaveAssets();
            }
        }

        private static void BuildApk(string path, string applicationId, BuildOptions options = BuildOptions.Development)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Select Android as the active build platform first.");
            EditorSceneManager.OpenScene(ScenePath);
            Validate();
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != applicationId)
                throw new BuildFailedException("The selected app ID does not match this build.");
            if (!((PluginImporter)AssetImporter.GetAtPath(PluginPath)).GetCompatibleWithPlatform(BuildTarget.Android))
                throw new BuildFailedException("Enable the shared Android photo library using Add Missing Photo Interface.");
            Directory.CreateDirectory("Release_APK");
            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            bool oldExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, target = BuildTarget.Android,
                    locationPathName = path, options = options
                });
                if (result.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Notes photo build failed.");
                Debug.Log("NOTE_PHOTO_BUILD_SUCCESS: " + Path.GetFullPath(path));
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExport;
            }
        }

        public static void RenderPreview()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NotesApp>(true)).Single();
            var data = new SerializedObject(app);
            var navigator = new SerializedObject(app.GetComponent<NotesPageNavigator>());
            ((GameObject)navigator.FindProperty("libraryPage").objectReferenceValue).SetActive(false);
            ((GameObject)navigator.FindProperty("notesPage").objectReferenceValue).SetActive(false);
            ((GameObject)navigator.FindProperty("editorPage").objectReferenceValue).SetActive(true);
            ((TMP_InputField)data.FindProperty("titleField").objectReferenceValue).SetTextWithoutNotify("Xmas gift for wife");
            ((TMP_InputField)data.FindProperty("bodyField").objectReferenceValue).SetTextWithoutNotify("A picture of the gift goes below this text. Tap the preview to open it.");
            ((TMP_InputField)data.FindProperty("titleField").objectReferenceValue).ForceLabelUpdate();
            ((TMP_InputField)data.FindProperty("bodyField").objectReferenceValue).ForceLabelUpdate();
            var photos = new SerializedObject(app.GetComponent<NotePhotoController>());
            ((GameObject)photos.FindProperty("previewFrame").objectReferenceValue).SetActive(true);
            ((RawImage)photos.FindProperty("preview").objectReferenceValue).enabled = false;
            ((TMP_Text)((GameObject)photos.FindProperty("missingImage").objectReferenceValue).GetComponent<TMP_Text>()).text = "Picture preview";
            var canvas = app.GetComponent<Canvas>();
            var cameraObject = new GameObject("Temporary preview camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            var target = new RenderTexture(720, 1280, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var oldPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var oldQualityPipeline = QualitySettings.renderPipeline;
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            try
            {
                Directory.CreateDirectory("Release_APK");
                Capture(camera, target, "Release_APK/NotesPhotos-preview.png");
                ((GameObject)photos.FindProperty("replacementDialog").objectReferenceValue).SetActive(true);
                Capture(camera, target, "Release_APK/NotesPhotos-confirmation.png");
            }
            finally
            {
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = oldPipeline;
                QualitySettings.renderPipeline = oldQualityPipeline;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        internal static void Capture(Camera camera, RenderTexture target, string path)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous;
            Object.DestroyImmediate(texture);
        }
    }
}
