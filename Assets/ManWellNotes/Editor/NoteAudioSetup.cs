using System;
using System.IO;
using System.Linq;
using ManWellNotes;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ManWellNotesEditor.NotePhotoSetup;
using Object = UnityEngine.Object;

namespace ManWellNotesEditor
{
    public static class NoteAudioSetup
    {
        private const string Root = "Assets/ManWellNotes/Prefabs/";
        [MenuItem("Tools/Man-Well Notes/Sound/Add Missing Recording Interface")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var app = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NotesApp>(true)).Single();
            var appData = new SerializedObject(app);
            var nav = new SerializedObject(app.GetComponent<NotesPageNavigator>());
            var font = (TMP_FontAsset)appData.FindProperty("font").objectReferenceValue;
            var controller = app.GetComponent<NoteAudioController>() ?? Undo.AddComponent<NoteAudioController>(app.gameObject);
            EnsurePrefab(Root + "Record Sound.prefab", () => Button("Record Sound", null, font).gameObject);
            EnsurePrefab(Root + "Note Sound.prefab", () => BuildSection(font));
            EnsurePrefab(Root + "Recording Status.prefab", () => BuildStatus(font));
            EnsurePrefab(Root + "Sound Dialogs.prefab", () => {
                var root = Rect("Sound Dialogs", null); Stretch(root);
                Dialog(root, "Remove", "Remove this sound from the note?", new[] { "Yes", "No" }, font);
                return root.gameObject;
            });
            var data = new SerializedObject(controller);
            if (data.FindProperty("recordButton").objectReferenceValue == null)
            {
                var newNote = app.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "New note");
                int index = newNote.transform.GetSiblingIndex();
                var row = Row("Create note actions", newNote.transform.parent, 48);
                Undo.RegisterCreatedObjectUndo(row.gameObject, "Add recording action row");
                row.SetSiblingIndex(index);
                Undo.SetTransformParent(newNote.transform, row, "Place new note beside RS");
                var layout = newNote.GetComponent<LayoutElement>();
                Undo.RecordObject(layout, "Size new note action");
                layout.flexibleWidth = 1;
                var record = Instance("Record Sound", row);
                var size = record.gameObject.AddComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = 48;
                size.flexibleWidth = 0;
                record.GetComponentInChildren<TMP_Text>().text = "RS";
                PrefabUtility.RecordPrefabInstancePropertyModifications(record.GetComponentInChildren<TMP_Text>());
                Assign(data, "recordButton", record.GetComponent<UnityEngine.UI.Button>());
                Wire(row, "Record Sound", controller.RecordSound);
            }
            var editor = ((GameObject)appData.FindProperty("editorPanel").objectReferenceValue).transform;
            Transform section = Existing(data, "section") ?? Instance("Note Sound", editor);
            if (data.FindProperty("section").objectReferenceValue == null)
            {
                var picture = editor.Find("Note Picture");
                section.SetSiblingIndex(picture.GetSiblingIndex() + 1);
            }
            Transform dialogs = app.transform.Find("Sound Dialogs") ?? Instance("Sound Dialogs", app.transform);
            Stretch((RectTransform)dialogs);
            PrefabUtility.RecordPrefabInstancePropertyModifications(dialogs);
            Assign(data, "app", app);
            Assign(data, "messages", app.GetComponent<NotePhotoController>());
            Assign(data, "section", section.gameObject);
            Assign(data, "statusText", section.Find("Status").GetComponent<TMP_Text>());
            Assign(data, "playButton", section.Find("Actions/Play").GetComponent<UnityEngine.UI.Button>());
            Assign(data, "stopPlaybackButton", section.Find("Actions/Stop").GetComponent<UnityEngine.UI.Button>());
            Assign(data, "removeButton", section.Find("Actions/Remove").GetComponent<UnityEngine.UI.Button>());
            Assign(data, "removeDialog", dialogs.Find("Remove").gameObject);
            Wire(section, "Actions/Play", controller.Play);
            Wire(section, "Actions/Stop", controller.StopPlayback);
            Wire(section, "Actions/Remove", controller.AskRemove);
            Wire(dialogs, "Remove/Panel/Buttons/Yes", controller.ConfirmRemove);
            Wire(dialogs, "Remove/Panel/Buttons/No", controller.CloseDialog);
            if (data.FindProperty("banners").arraySize == 0)
            {
                foreach (string array in new[] { "banners", "timerTexts", "stopRecordingButtons", "retryButtons" }) data.FindProperty(array).arraySize = 3;
                string[] pages = { "libraryPage", "notesPage", "editorPage" };
                for (int i = 0; i < pages.Length; i++)
                {
                    var page = ((GameObject)nav.FindProperty(pages[i]).objectReferenceValue).transform;
                    var strip = Instance("Recording Status", page);
                    strip.SetAsFirstSibling();
                    SetAt(data, "banners", i, strip.gameObject);
                    SetAt(data, "timerTexts", i, strip.Find("Timer").GetComponent<TMP_Text>());
                    SetAt(data, "stopRecordingButtons", i, strip.Find("Stop").GetComponent<UnityEngine.UI.Button>());
                    SetAt(data, "retryButtons", i, strip.Find("Retry save").GetComponent<UnityEngine.UI.Button>());
                    Wire(strip, "Stop", controller.StopRecording);
                    Wire(strip, "Retry save", controller.RetrySave);
                }
            }
            data.ApplyModifiedProperties();
            Assign(appData, "audioController", controller); appData.ApplyModifiedProperties();
            Assign(nav, "audioController", controller); nav.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ValidateAudio();
        }
        private static Transform Existing(SerializedObject data, string field) => (data.FindProperty(field).objectReferenceValue as GameObject)?.transform;
        private static void SetAt(SerializedObject data, string field, int index, Object value) => data.FindProperty(field).GetArrayElementAtIndex(index).objectReferenceValue = value;
        private static Transform Instance(string name, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + name + ".prefab"), parent);
            Undo.RegisterCreatedObjectUndo(go, "Add recording interface");
            return go.transform;
        }
        private static GameObject BuildSection(TMP_FontAsset font)
        {
            var root = Rect("Note Sound", null);
            var group = root.gameObject.AddComponent<VerticalLayoutGroup>();
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            group.spacing = 4;
            Label("Status", "Sound", root, font, 16).gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            var actions = Row("Actions", root, 36);
            foreach (string name in new[] { "Play", "Stop", "Remove" }) Button(name, actions, font);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }
        private static GameObject BuildStatus(TMP_FontAsset font)
        {
            var row = Row("Recording Status", null, 38);
            row.gameObject.AddComponent<Image>().color = new Color32(245, 245, 245, 255);
            var label = Label("Timer", "Recording 00:00:00", row, font, 14);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            foreach (string name in new[] { "Stop", "Retry save" })
            {
                var b = Button(name, row, font);
                var size = b.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = 70;
                size.flexibleWidth = 0;
                if (name == "Retry save") b.gameObject.SetActive(false);
            }
            row.gameObject.SetActive(false);
            return row.gameObject;
        }
        [MenuItem("Tools/Man-Well Notes/Sound/Validate Recording Interface")]
        public static void ValidateAudio()
        {
            var controller = Resources.FindObjectsOfTypeAll<NoteAudioController>().Single(c => c.gameObject.scene.path == ScenePath);
            var data = new SerializedObject(controller);
            var iterator = data.GetIterator();
            while (iterator.NextVisible(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null)
                    throw new BuildFailedException("Missing audio reference: " + iterator.propertyPath);
            foreach (string name in new[] { "Record Sound", "Note Sound", "Sound Dialogs", "Recording Status" })
            {
                var instances = controller.GetComponentsInChildren<Transform>(true).Where(t =>
                    PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == Root + name + ".prefab").ToArray();
                if (instances.Length != (name == "Recording Status" ? 3 : 1)) throw new BuildFailedException("Duplicated or missing audio prefab: " + name);
                foreach (var instance in instances)
                    foreach (var button in instance.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                        if (button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != controller)
                            throw new BuildFailedException("Incorrect audio button wiring: " + button.name);
            }
            Debug.Log("NOTE_AUDIO_UI_VALIDATED");
        }
        public static void VerifyAndBuild()
        {
            NotePhotoSetup.Apply();
            Apply(); Apply();
            NotePhotoValidation.Run();
            NoteAudioValidation.Run();
            RenderAudioPreview();
            NotePhotoSetup.BuildPreviewAndroid();
        }

        public static void RenderAudioPreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var app = Resources.FindObjectsOfTypeAll<NotesApp>().Single(a => a.gameObject.scene.path == ScenePath);
            var nav = new SerializedObject(app.GetComponent<NotesPageNavigator>());
            var data = new SerializedObject(app);
            var audio = new SerializedObject(app.GetComponent<NoteAudioController>());
            var library = (GameObject)nav.FindProperty("libraryPage").objectReferenceValue;
            var notes = (GameObject)nav.FindProperty("notesPage").objectReferenceValue;
            var editor = (GameObject)nav.FindProperty("editorPage").objectReferenceValue;
            library.SetActive(true); notes.SetActive(false); editor.SetActive(false);
            var cameraObject = new GameObject("Temporary audio preview camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            var target = new RenderTexture(720, 1280, 24); camera.targetTexture = target;
            var canvas = app.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var quality = QualitySettings.renderPipeline;
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
            try
            {
                Directory.CreateDirectory("Release_APK");
                Capture(camera, target, "Release_APK/NotesAudio-library.png");
                library.SetActive(false); editor.SetActive(true);
                var title = (TMP_InputField)data.FindProperty("titleField").objectReferenceValue;
                title.SetTextWithoutNotify("2026_10_27_09_05"); title.ForceLabelUpdate();
                var body = (TMP_InputField)data.FindProperty("bodyField").objectReferenceValue;
                body.SetTextWithoutNotify(""); body.ForceLabelUpdate();
                var photo = new SerializedObject(app.GetComponent<NotePhotoController>());
                ((UnityEngine.UI.Button)photo.FindProperty("removeButton").objectReferenceValue).gameObject.SetActive(false);
                ((GameObject)audio.FindProperty("banners").GetArrayElementAtIndex(2).objectReferenceValue).SetActive(true);
                Capture(camera, target, "Release_APK/NotesAudio-recording.png");
                ((GameObject)audio.FindProperty("banners").GetArrayElementAtIndex(2).objectReferenceValue).SetActive(false);
                ((GameObject)audio.FindProperty("section").objectReferenceValue).SetActive(true);
                ((TMP_Text)audio.FindProperty("statusText").objectReferenceValue).text = "Sound · 00:01:23";
                Capture(camera, target, "Release_APK/NotesAudio-saved.png");
            }
            finally
            {
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = quality;
                camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
                EditorSceneManager.OpenScene(ScenePath);
            }
        }
    }
}
