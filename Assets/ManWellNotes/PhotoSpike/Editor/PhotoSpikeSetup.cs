using System;
using System.IO;
using System.Linq;
using ManWellNotes.PhotoSpike;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ManWellNotesEditor.PhotoSpike
{
    // Explicit Editor commands only. Never generates or restyles UI during Play Mode.
    public static class PhotoSpikeSetup
    {
        public const string Root = "Assets/ManWellNotes/PhotoSpike";
        public const string ScenePath = Root + "/Scenes/Photo Test.unity";
        public const string PrefabPath = Root + "/Prefabs/Photo Test UI.prefab";
        public const string PluginPath = Root + "/Plugins/Android/PhotoSpike.androidlib";

        [MenuItem("Tools/Man-Well Notes/Photo Test/Open Test Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateMissingAssets();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Man-Well Notes/Photo Test/Create Missing Test Assets")]
        public static void CreateMissingAssets()
        {
            if (File.Exists(PrefabPath) && File.Exists(ScenePath))
            {
                AddMissingReplacementDialog();
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();
            Scene previous = SceneManager.GetActiveScene();
            // A fresh batch project has one untitled scene; Unity cannot add a scene beside it.
            bool replaceEmptyBatchScene = Application.isBatchMode && string.IsNullOrEmpty(previous.path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceEmptyBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            if (!File.Exists(PrefabPath))
            {
                GameObject root = BuildUi();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
            }
            if (!File.Exists(ScenePath))
            {
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(events, scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            if (!replaceEmptyBatchScene)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Photo test assets are ready. Existing scene and prefab edits were preserved.");
        }

        private static GameObject BuildUi()
        {
            var root = new GameObject("Photo Test UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PhotoSpikeController));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(360, 640);
            scaler.matchWidthOrHeight = 0.5f;

            var background = Rect("Background", root.transform);
            Stretch(background);
            background.gameObject.AddComponent<Image>().color = new Color32(247, 247, 242, 255);
            var page = Rect("Page", background);
            Stretch(page, 20, 38, 20, 28);
            var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Label("Title", "Notes Photo Test", page, 26, 36, FontStyle.Bold);
            Label("Introduction", "Take a photo or choose an image.\nThis separate test keeps one image at a time.", page, 15, 42);

            var row = Rect("Photo actions", page);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 10;
            horizontal.childControlWidth = horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = true;
            Button camera = Button("Take photo", row);
            Button picker = Button("Choose image", row);

            var frame = Rect("Preview frame", page);
            frame.gameObject.AddComponent<Image>().color = new Color32(225, 229, 225, 255);
            var frameSize = frame.gameObject.AddComponent<LayoutElement>();
            frameSize.minHeight = 100;
            frameSize.preferredHeight = 200;
            frameSize.flexibleHeight = 1;
            var hint = Label("Empty hint", "Image preview", frame, 18, 0);
            hint.alignment = TextAnchor.MiddleCenter;
            Stretch(hint.rectTransform);
            var viewport = Rect("Image viewport", frame);
            Stretch(viewport, 8, 8, 8, 8);
            var imageRect = Rect("Photo", viewport);
            var image = imageRect.gameObject.AddComponent<RawImage>();
            image.enabled = false;
            image.raycastTarget = false;
            var aspect = imageRect.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1;

            Text status = Label("Status", "Ready for a photo. Camera and picker access are tested on Android.", page, 14, 100);
            status.alignment = TextAnchor.UpperLeft;
            Button clear = Button("Clear test image", page);
            clear.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            Button reset = Button("Reset interrupted request", page);
            reset.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
            reset.GetComponent<Image>().color = new Color32(93, 101, 96, 255);

            var controller = root.GetComponent<PhotoSpikeController>();
            UnityEventTools.AddPersistentListener(camera.onClick, controller.TakePhoto);
            UnityEventTools.AddPersistentListener(picker.onClick, controller.ChooseImage);
            UnityEventTools.AddPersistentListener(clear.onClick, controller.ClearImage);
            UnityEventTools.AddPersistentListener(reset.onClick, controller.ResetInterruptedRequest);
            var data = new SerializedObject(controller);
            data.FindProperty("preview").objectReferenceValue = image;
            data.FindProperty("previewAspect").objectReferenceValue = aspect;
            data.FindProperty("emptyHint").objectReferenceValue = hint.gameObject;
            data.FindProperty("status").objectReferenceValue = status;
            data.FindProperty("takePhotoButton").objectReferenceValue = camera;
            data.FindProperty("chooseImageButton").objectReferenceValue = picker;
            data.FindProperty("clearButton").objectReferenceValue = clear;
            data.ApplyModifiedPropertiesWithoutUndo();
            BuildReplacementDialog(root);
            return root;
        }

        [MenuItem("Tools/Man-Well Notes/Photo Test/Add Missing Replacement Dialog")]
        public static void AddMissingReplacementDialog()
        {
            if (!File.Exists(PrefabPath)) throw new BuildFailedException("Create the test prefab first.");
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Existing layout and any previously edited confirmation are never regenerated.
                if (contents.transform.Find("Replacement confirmation") != null) return;
                BuildReplacementDialog(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void BuildReplacementDialog(GameObject root)
        {
            var overlay = Rect("Replacement confirmation", root.transform);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
            var panel = Rect("Panel", overlay);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(300, 180);
            panel.gameObject.AddComponent<Image>().color = new Color32(247, 247, 242, 255);
            var content = Rect("Content", panel);
            Stretch(content, 20, 20, 20, 20);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Text message = Label("Message", "The existing picture will be replaced", content, 20, 64);
            message.alignment = TextAnchor.MiddleCenter;
            var row = Rect("Buttons", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            var buttons = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            buttons.spacing = 12;
            buttons.childControlWidth = buttons.childControlHeight = true;
            buttons.childForceExpandWidth = buttons.childForceExpandHeight = true;
            Button yes = Button("Yes", row);
            Button no = Button("No", row);
            no.GetComponent<Image>().color = new Color32(93, 101, 96, 255);
            var controller = root.GetComponent<PhotoSpikeController>();
            UnityEventTools.AddPersistentListener(yes.onClick, controller.ConfirmReplacement);
            UnityEventTools.AddPersistentListener(no.onClick, controller.CancelReplacement);
            var data = new SerializedObject(controller);
            data.FindProperty("replacementDialog").objectReferenceValue = overlay.gameObject;
            data.FindProperty("replacementNoButton").objectReferenceValue = no;
            data.ApplyModifiedPropertiesWithoutUndo();
            overlay.gameObject.SetActive(false);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Text Label(string name, string value, Transform parent, int fontSize, float height, FontStyle style = FontStyle.Normal)
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = new Color32(31, 40, 35, 255);
            label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            if (height > 0) rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        private static Button Button(string name, Transform parent)
        {
            var rect = Rect(name, parent);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color32(39, 70, 52, 255);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var label = Label("Label", name, rect, 16, 0);
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, 6, 4, 6, 4);
            return button;
        }

        [MenuItem("Tools/Man-Well Notes/Photo Test/Validate Saved Test Assets")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || !File.Exists(ScenePath)) throw new BuildFailedException("Create the photo test scene and prefab first.");
            var controller = prefab.GetComponent<PhotoSpikeController>();
            if (controller == null) throw new BuildFailedException("Photo Test UI needs its PhotoSpikeController.");
            var data = new SerializedObject(controller);
            foreach (string field in new[] { "preview", "previewAspect", "emptyHint", "status", "takePhotoButton", "chooseImageButton", "clearButton", "replacementDialog", "replacementNoButton" })
                if (data.FindProperty(field).objectReferenceValue == null) throw new BuildFailedException("Assign Photo Test UI / " + field);
            foreach (Button button in prefab.GetComponentsInChildren<Button>(true))
                if (button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != controller)
                    throw new BuildFailedException("Wire exactly one controller action on " + button.name);
            Debug.Log("Photo test validation passed: saved references and Inspector button events are assigned.");
        }

        [MenuItem("Tools/Man-Well Notes/Photo Test/Build Android Test APK")]
        public static void BuildAndroid()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateMissingAssets();
            Validate();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch the active build platform to Android before building the photo test.");
            var plugin = AssetImporter.GetAtPath(PluginPath) as PluginImporter;
            if (plugin == null) throw new BuildFailedException("The photo test Android library was not imported.");
            var target = NamedBuildTarget.Android;
            string oldId = PlayerSettings.GetApplicationIdentifier(target);
            string oldProduct = PlayerSettings.productName;
            var oldBackend = PlayerSettings.GetScriptingBackend(target);
            var oldArchitecture = PlayerSettings.Android.targetArchitectures;
            var oldEntry = PlayerSettings.Android.applicationEntry;
            var oldMinSdk = PlayerSettings.Android.minSdkVersion;
            bool oldKeystore = PlayerSettings.Android.useCustomKeystore;
            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            bool oldExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            bool oldCompatibility = plugin.GetCompatibleWithPlatform(BuildTarget.Android);
            try
            {
                PlayerSettings.SetApplicationIdentifier(target, "com.manwell.notes.phototest");
                PlayerSettings.productName = "Notes Photo Test";
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                plugin.SetCompatibleWithPlatform(BuildTarget.Android, true);
                plugin.SaveAndReimport();
                Directory.CreateDirectory("Release_APK");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, target = BuildTarget.Android,
                    locationPathName = "Release_APK/NotesPhotoTest.apk",
                    options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "MANWELL_PHOTO_SPIKE" }
                });
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Photo test APK build failed. See the Unity Console.");
                Debug.Log("PHOTO_SPIKE_BUILD_SUCCESS: " + Path.GetFullPath("Release_APK/NotesPhotoTest.apk"));
            }
            finally
            {
                plugin.SetCompatibleWithPlatform(BuildTarget.Android, oldCompatibility);
                plugin.SaveAndReimport();
                PlayerSettings.SetApplicationIdentifier(target, oldId);
                PlayerSettings.productName = oldProduct;
                PlayerSettings.SetScriptingBackend(target, oldBackend);
                PlayerSettings.Android.targetArchitectures = oldArchitecture;
                PlayerSettings.Android.applicationEntry = oldEntry;
                PlayerSettings.Android.minSdkVersion = oldMinSdk;
                PlayerSettings.Android.useCustomKeystore = oldKeystore;
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExport;
                AssetDatabase.SaveAssets();
            }
        }

        // Used by the isolated batch validation project; also useful for checking authored layout.
        public static void RenderPreview() => Render(false);
        public static void RenderReplacementPreview() => Render(true);

        private static void Render(bool showReplacement)
        {
            CreateMissingAssets();
            Validate();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            var canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>()).First();
            if (showReplacement) canvas.transform.Find("Replacement confirmation").gameObject.SetActive(true);
            var cameraObject = new GameObject("Temporary preview camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(720, 1280, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(720, 1280, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Release_APK");
            File.WriteAllBytes(showReplacement ? "Release_APK/PhotoTest-replacement-preview.png" : "Release_APK/PhotoTest-preview.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            // This preview never saves its temporary camera or canvas changes into the scene.
        }

        public static void ValidateReplacementFlow()
        {
            CreateMissingAssets();
            Validate();
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var first = new Texture2D(8, 4);
            var second = new Texture2D(4, 8);
            try
            {
                var controller = instance.GetComponent<PhotoSpikeController>();
                typeof(PhotoSpikeController).GetMethod("Start", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(controller, null);
                var data = new SerializedObject(controller);
                var image = (RawImage)data.FindProperty("preview").objectReferenceValue;
                var dialog = (GameObject)data.FindProperty("replacementDialog").objectReferenceValue;
                data.FindProperty("editorSample").objectReferenceValue = first;
                data.ApplyModifiedPropertiesWithoutUndo();
                controller.ChooseImage();
                Require(image.texture == first && !dialog.activeSelf, "First picture must not prompt.");
                data.FindProperty("editorSample").objectReferenceValue = second;
                data.ApplyModifiedPropertiesWithoutUndo();
                controller.ChooseImage();
                Require(dialog.activeSelf && image.texture == first, "Replacement must await confirmation.");
                controller.CancelReplacement();
                Require(!dialog.activeSelf && image.texture == first, "No must keep the existing picture.");
                controller.ConfirmReplacement();
                Require(image.texture == first, "A stale Yes must not replace the picture.");
                controller.ChooseImage();
                controller.ConfirmReplacement();
                Require(!dialog.activeSelf && image.texture == second, "Yes must continue the selected source.");
                controller.TakePhoto();
                Require(dialog.activeSelf, "Camera replacement must also prompt.");
                controller.ConfirmReplacement();
                Require(image.texture == second, "A source without a new picture must keep the existing one.");
                controller.ClearImage();
                controller.ChooseImage();
                Require(!dialog.activeSelf && image.texture == second, "After clearing, adding must not prompt.");
                Debug.Log("PHOTO_REPLACEMENT_CHECKS_PASSED: first image, No, stale Yes, Yes, camera, no-result preservation, clear then add.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }

        public static void VerifyReplacementAndBuild()
        {
            if (!Application.isBatchMode) throw new BuildFailedException("Use the individual Photo Test menu commands in the Editor.");
            ValidateReplacementFlow();
            RenderReplacementPreview();
            EditorSceneManager.OpenScene(ScenePath); // Discard the temporary preview camera settings.
            BuildAndroid();
        }
    }

    public sealed class PhotoSpikeBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var plugin = AssetImporter.GetAtPath(PhotoSpikeSetup.PluginPath) as PluginImporter;
            if (plugin != null && plugin.GetCompatibleWithPlatform(BuildTarget.Android) &&
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.manwell.notes.phototest" &&
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.manwell.notes" &&
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.manwell.notes.photospreview")
                throw new BuildFailedException("The shared photo library is enabled for an unexpected app ID. Verify the intended app before building.");
        }
    }
}
