using ManWellNotes;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace ManWellNotesEditor
{
    // This editor-only utility turns the UI description in NotesApp into a normal,
    // saved GameObject hierarchy. It can be run from the menu whenever you want to
    // restore the original layout after experimenting with the scene.
    public static class NotesSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootName = "Man-Well Notes";
        private const string PinnedIconBlackPath =
            "Assets/ManWellNotes/Icons/General Icons/Set 1/1024 px/Black/Untitled-1_0000s_0003_Layer-32.png";
        private const string PinnedIconWhitePath =
            "Assets/ManWellNotes/Icons/General Icons/Set 1/1024 px/Set-1-_-1024px_0000s_0003_Layer-32.png";

        [MenuItem("Tools/Man-Well Notes/Rebuild Notes Interface")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Rebuilding is intentional: remove only the app root and leave cameras,
            // lighting, and any unrelated objects in the scene untouched.
            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null && oldRoot.GetComponent<NotePhotoController>() != null)
                throw new System.InvalidOperationException("This scene has an editable photo interface. Edit its saved prefabs or use Photos/Add Missing Photo Interface; rebuilding the old interface would remove it.");
            TMP_FontAsset primaryFont = NotesFontBuilder.BuildPrimaryFont();
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot);

            GameObject root = new GameObject(RootName, typeof(RectTransform));
            NotesApp app = root.AddComponent<NotesApp>();
            app.BuildSceneInterface(primaryFont);
            AssignPinnedIcons(app);

            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = root;
            Debug.Log("Built the editable Man-Well Notes interface in SampleScene.");
        }

        public static void AssignPinnedIcons(NotesApp app)
        {
            Sprite black = AssetDatabase.LoadAllAssetsAtPath(PinnedIconBlackPath)
                .OfType<Sprite>()
                .FirstOrDefault();
            Sprite white = AssetDatabase.LoadAllAssetsAtPath(PinnedIconWhitePath)
                .OfType<Sprite>()
                .FirstOrDefault();
            if (black == null || white == null)
                throw new System.InvalidOperationException("The starred-note icon sprites could not be loaded.");

            SerializedObject data = new SerializedObject(app);
            data.FindProperty("pinnedIconBlack").objectReferenceValue = black;
            data.FindProperty("pinnedIconWhite").objectReferenceValue = white;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
