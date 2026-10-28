using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ManWellNotes;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ManWellNotesEditor
{
    public static class NoteUiVisibilityChecks
    {
        [MenuItem("Tools/Man-Well Notes/Validate Photo and Sound Visibility")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before validating saved UI.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(NotePhotoSetup.ScenePath);
            NotePhotoSetup.Validate();
            NoteAudioSetup.ValidateAudio();
            var app = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NotesApp>(true)).Single();
            var photos = app.GetComponent<NotePhotoController>();
            var audio = app.GetComponent<NoteAudioController>();
            try
            {
                // Exercise the Editor branches with an in-memory note; never load/save the owner's notes.
                Set(app, "store", new NoteStore(Path.GetFullPath(".utmp/VisibilityChecks")));
                Set(app, "selectedNote", NoteData.Create());
                Set(photos, "ready", true);
                Set(audio, "initialized", true);
                Set(audio, "stateKnown", true);
                var editor = (GameObject)Get(app, "editorPanel");
                editor.SetActive(true);
                typeof(NotePhotoController).GetMethod("RefreshAvailability", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(photos, null);
                Check(((UnityEngine.UI.Button)Get(photos, "addButton")).gameObject.activeInHierarchy, "Add picture is hidden");
                photos.OpenSource();
                Check(((GameObject)Get(photos, "sourceDialog")).activeInHierarchy, "Photo source dialog is hidden");
                photos.CloseDialogs();
                audio.RecordSound();
                Check(((GameObject)Get(photos, "messageDialog")).activeInHierarchy, "RS Editor explanation is hidden");
                Check(((TMPro.TMP_Text)Get(photos, "messageText")).text.Contains("Android"), "RS should explain Android recording in the Editor");
                Check(!((GameObject)Get(audio, "removeDialog")).activeInHierarchy, "Sound removal dialog must start hidden");
                Debug.Log("NOTE_UI_VISIBILITY_CHECKS_PASSED: 5");
            }
            finally { EditorSceneManager.OpenScene(NotePhotoSetup.ScenePath); }
        }
        private static object Get(object o, string field) => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
        private static void Check(bool condition, string message) { if (!condition) throw new BuildFailedException(message); }
    }
}
