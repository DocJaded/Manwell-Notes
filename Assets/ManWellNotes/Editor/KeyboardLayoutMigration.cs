using System.Linq;
using ManWellNotes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ManWellNotesEditor
{
    /// <summary>
    /// Applies only the keyboard-related layout changes to the existing scene.
    /// It deliberately preserves every existing page and control.
    /// </summary>
    public static class KeyboardLayoutMigration
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Man-Well Notes/Apply Keyboard-Safe Layout")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject appPanel = RequireObject("App");
            GameObject editorPage = RequireObject("Editor");
            GameObject toolbar = RequireObject("Toolbar");

            if (toolbar.transform.parent != editorPage.transform)
                throw new System.InvalidOperationException("The editor Toolbar is not a direct child of Editor.");

            // A VerticalLayoutGroup displays children in sibling order. Keeping the
            // same toolbar and moving it last places Back, Pin and Delete at the bottom.
            toolbar.transform.SetAsLastSibling();

            // This is a normal serialized scene component. At runtime it only changes
            // the existing App panel's bottom inset while Android's keyboard is visible.
            KeyboardSafeArea keyboardSafeArea = appPanel.GetComponent<KeyboardSafeArea>();
            if (keyboardSafeArea == null)
                keyboardSafeArea = appPanel.AddComponent<KeyboardSafeArea>();

            EditorUtility.SetDirty(toolbar);
            EditorUtility.SetDirty(appPanel);
            EditorUtility.SetDirty(keyboardSafeArea);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Moved the editor toolbar to the bottom and enabled keyboard-safe page resizing.");
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
    }
}
