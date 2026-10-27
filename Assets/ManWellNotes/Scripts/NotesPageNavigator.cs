using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ManWellNotes
{
    // Controls the three existing page GameObjects. No page is created at runtime;
    // this component only decides which saved scene object is currently visible.
    public sealed class NotesPageNavigator : MonoBehaviour
    {
        [Header("Saved scene pages")]
        [SerializeField] private GameObject libraryPage;
        [SerializeField] private GameObject notesPage;
        [SerializeField] private GameObject editorPage;

        [Header("Saved scene navigation buttons")]
        [SerializeField] private Button notesBackButton;
        [SerializeField] private Button editorBackButton;
        [SerializeField] private NotePhotoController photoController;
        [SerializeField] private NoteAudioController audioController;

        private Page currentPage;

        private enum Page
        {
            Library,
            Notes,
            Editor
        }

        private void Awake()
        {
            notesBackButton.onClick.AddListener(ShowLibraryPage);
            editorBackButton.onClick.AddListener(ShowNotesPage);
            ShowLibraryPage();
        }

        private void Update()
        {
            // Android maps the system Back gesture/button to Escape in Unity's
            // Input System. Move backward through the page stack before exiting.
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            if (photoController != null && photoController.TryCloseTopDialog()) return;
            if (audioController != null && audioController.TryCloseDialog()) return;

            switch (currentPage)
            {
                case Page.Editor:
                    ShowNotesPage();
                    break;
                case Page.Notes:
                    ShowLibraryPage();
                    break;
                default:
                    Application.Quit();
                    break;
            }
        }

        public void ShowLibraryPage() => Show(Page.Library);

        public void ShowNotesPage() => Show(Page.Notes);

        public void ShowEditorPage() => Show(Page.Editor);

        private void Show(Page page)
        {
            photoController?.CloseDialogs();
            audioController?.CloseDialog();
            currentPage = page;
            libraryPage.SetActive(page == Page.Library);
            notesPage.SetActive(page == Page.Notes);
            editorPage.SetActive(page == Page.Editor);
        }
    }
}
