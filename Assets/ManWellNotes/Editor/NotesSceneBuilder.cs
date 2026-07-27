using ManWellNotes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ManWellNotesEditor
{
    // This editor-only utility turns the UI description in NotesApp into a normal,
    // saved GameObject hierarchy. It can be run from the menu whenever you want to
    // restore the original layout after experimenting with the scene.
    public static class NotesSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootName = "Man-Well Notes";

        [MenuItem("Tools/Man-Well Notes/Rebuild Notes Interface")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Rebuilding is intentional: remove only the app root and leave cameras,
            // lighting, and any unrelated objects in the scene untouched.
            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot);

            GameObject root = new GameObject(RootName, typeof(RectTransform));
            NotesApp app = root.AddComponent<NotesApp>();
            app.BuildSceneInterface();

            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = root;
            Debug.Log("Built the editable Man-Well Notes interface in SampleScene.");
        }
    }
}
