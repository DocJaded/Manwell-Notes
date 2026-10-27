using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ManWellNotes
{
    // Visual hierarchy, labels, button events and layout are all saved Unity assets.
    public sealed class NotePhotoController : MonoBehaviour
    {
        [SerializeField] private NotesApp app;
        [Header("Saved picture section")]
        [SerializeField] private GameObject section;
        [SerializeField] private GameObject previewFrame;
        [SerializeField] private RawImage preview;
        [SerializeField] private AspectRatioFitter previewAspect;
        [SerializeField] private GameObject missingImage;
        [SerializeField] private Button addButton;
        [SerializeField] private Button viewButton;
        [SerializeField] private Button removeButton;
        [Header("Saved dialogs")]
        [SerializeField] private GameObject sourceDialog;
        [SerializeField] private GameObject replacementDialog;
        [SerializeField] private Button replacementNo;
        [SerializeField] private GameObject removeDialog;
        [SerializeField] private GameObject viewerDialog;
        [SerializeField] private RawImage fullImage;
        [SerializeField] private AspectRatioFitter fullAspect;
        [SerializeField] private GameObject messageDialog;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private GameObject pendingPanel;
        [SerializeField] private GameObject retryButton;
        [Header("Image settings")]
        [SerializeField, Range(256, 2048)] private int maximumImageSize = 2048;
        [SerializeField, Range(64, 512)] private int thumbnailSize = 320;
        [SerializeField] private bool hidePreviewWhileTyping = true;

        private NoteImageStore images;
        private PendingNotePhoto pending;
        private bool ready;
        private bool recoveryError;
        private bool awaitingRetry;
        private float nextPoll;
        private string dialogNoteId;
        private string expectedPhotoId;
        private string sourceChoice;
        private Texture2D previewTexture;
        private Texture2D viewerTexture;

        private void Start()
        {
            UnityEngine.Object[] required = { app, section, previewFrame, preview, previewAspect, missingImage,
                addButton, viewButton, removeButton, sourceDialog, replacementDialog, replacementNo, removeDialog,
                viewerDialog, fullImage, fullAspect, messageDialog, messageText, pendingPanel, retryButton };
            foreach (var reference in required)
                if (reference == null)
                {
                    Debug.LogError("Assign all NotePhotoController references in the saved scene.", this);
                    enabled = false;
                    return;
                }
            images = new NoteImageStore(Application.persistentDataPath);
            ready = true;
            app.CurrentNoteChanged += RefreshSelected;
            try { pending = images.ReadPending(); }
            catch { recoveryError = true; }
            RefreshSelected();
            if (recoveryError) ShowMessage("An unfinished picture request could not be restored. Cancel the request to try again. Your existing pictures are unchanged.");
            if (!app.Store.LoadSucceeded) ShowMessage(app.Store.LastError);
            CleanupFiles();
        }

        private void Update()
        {
            if (!ready) return;
            RefreshAvailability();
            if (pending == null || awaitingRetry || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.5f;
#if UNITY_ANDROID && !UNITY_EDITOR
            PollResult();
#endif
        }

        public void OpenSource()
        {
            if (!CanStart()) return;
            app.EndEditingForPhoto();
            CloseDialogs();
            dialogNoteId = app.SelectedNote.id;
            expectedPhotoId = NotePhotoData.Identity(app.SelectedNote.photo);
            sourceDialog.SetActive(true);
        }

        public void TakePhoto() => ChooseSource("camera");
        public void ChooseImage() => ChooseSource("picker");

        private void ChooseSource(string source)
        {
            if (!CanStart() || dialogNoteId == null) return;
            sourceChoice = source;
            sourceDialog.SetActive(false);
            var note = app.FindNote(dialogNoteId);
            if (note == null) { CloseDialogs(); return; }
            if (NotePhotoData.Exists(note.photo))
            {
                replacementDialog.SetActive(true);
                EventSystem.current?.SetSelectedGameObject(replacementNo.gameObject);
            }
            else LaunchSource();
        }

        public void ConfirmReplacement()
        {
            if (ready && replacementDialog.activeSelf && sourceChoice != null) LaunchSource();
        }

        private void LaunchSource()
        {
            string noteId = dialogNoteId, expected = expectedPhotoId, source = sourceChoice;
            CloseDialogs();
            var note = app.FindNote(noteId);
            if (note == null || NotePhotoData.Identity(note.photo) != expected)
            { ShowMessage("The note changed. Please choose the picture again."); return; }
            if (!app.TrySaveNotes()) { ShowMessage(app.Store.LastError); return; }
            try
            {
                pending = new PendingNotePhoto { id = Guid.NewGuid().ToString("N"), noteId = noteId, expectedPhotoId = expected };
                images.SavePending(pending);
#if UNITY_EDITOR
                if (source == "camera")
                {
                    CompleteRequest("Test camera capture on Android. In the Editor, Choose image accepts a PNG up to 2048 pixels per edge.");
                    return;
                }
                string path = UnityEditor.EditorUtility.OpenFilePanel("Choose a PNG (maximum 2048 pixels per edge)", "", "png");
                if (string.IsNullOrEmpty(path)) CompleteRequest(null);
                else AcceptResult(path);
#elif UNITY_ANDROID
                bool started = AndroidNotePhotos.Begin(source, maximumImageSize, pending.id);
                if (!started)
                {
                    recoveryError = true;
                    ShowMessage("A previous picture request is still active. Cancel the request before trying again.");
                }
#else
                CompleteRequest("Camera and image selection are available in the Android app.");
#endif
            }
            catch (Exception exception)
            {
                awaitingRetry = true;
                ShowMessage("The picture could not be added. " + exception.Message);
            }
            RefreshAvailability();
        }

        private void PollResult()
        {
            try
            {
                var state = AndroidNotePhotos.State();
                if (state == null) return;
                if (state.id != pending.id)
                {
                    if (!state.pending) CompleteRequest("The picture request was interrupted. Please try again.");
                    return;
                }
                if (state.pending) return;
                if (state.outcome == "success") AcceptResult(state.path);
                else if (state.outcome == "cancelled") CompleteRequest(null);
                else CompleteRequest("The picture was not changed. " + state.message);
            }
            catch (Exception exception)
            {
                awaitingRetry = true;
                ShowMessage("The picture could not be saved. " + exception.Message);
            }
        }

        private void AcceptResult(string path)
        {
            var note = app.FindNote(pending.noteId);
            if (note != null && NotePhotoData.Identity(note.photo) == pending.id)
            { CompleteRequest(null); return; } // A crash after commit must not attach twice.
            if (note == null || NotePhotoData.Identity(note.photo) != pending.expectedPhotoId)
            { CompleteRequest("The original note or picture changed. Its current picture was kept."); return; }
            var photo = images.Import(path, pending, thumbnailSize);
            if (!app.TrySetPhoto(pending.noteId, pending.expectedPhotoId, photo, out string error))
            {
                awaitingRetry = true;
                ShowMessage(error);
                return;
            }
            CompleteRequest(null);
        }

        private void CompleteRequest(string message)
        {
            string id = pending?.id;
            images.ClearPending();
            pending = null;
            recoveryError = awaitingRetry = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (id != null) AndroidNotePhotos.Acknowledge(id);
#endif
            RefreshSelected();
            CleanupFiles();
            if (!string.IsNullOrEmpty(message)) ShowMessage(message);
        }

        public void RetrySave()
        {
            if (!ready || pending == null) return;
            awaitingRetry = false;
#if UNITY_EDITOR
            // No device result survives an Editor file dialog; preserve the note and choose again.
            CompleteRequest("Please choose the image again.");
#endif
            messageDialog.SetActive(false);
        }

        public void CancelRequest()
        {
            if (!ready) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { AndroidNotePhotos.Cancel(); }
            catch (Exception exception) { ShowMessage(exception.Message); return; }
#endif
            // Already committed pictures are stored separately and are not removed here.
            try { CompleteRequest(null); }
            catch (Exception exception) { ShowMessage(exception.Message); }
        }

        public void AskRemove()
        {
            if (!CanStart() || !NotePhotoData.Exists(app.SelectedNote.photo)) return;
            CloseDialogs();
            dialogNoteId = app.SelectedNote.id;
            expectedPhotoId = NotePhotoData.Identity(app.SelectedNote.photo);
            removeDialog.SetActive(true);
        }

        public void ConfirmRemove()
        {
            if (!ready || !removeDialog.activeSelf || dialogNoteId == null) return;
            string noteId = dialogNoteId, expected = expectedPhotoId;
            CloseDialogs();
            if (!app.TrySetPhoto(noteId, expected, null, out string error)) ShowMessage(error);
            else CleanupFiles();
        }

        public void OpenViewer()
        {
            if (!ready || !NotePhotoData.Exists(app.SelectedNote?.photo)) return;
            try
            {
                CloseDialogs();
                viewerTexture = images.Load(app.SelectedNote.photo.imagePath);
                fullImage.texture = viewerTexture;
                fullAspect.aspectRatio = (float)viewerTexture.width / viewerTexture.height;
                viewerDialog.SetActive(true);
            }
            catch { ShowMessage("This picture could not be opened. You can replace it or remove it from the note."); }
        }

        public void CloseDialogs()
        {
            if (!ready) return;
            sourceDialog.SetActive(false);
            replacementDialog.SetActive(false);
            removeDialog.SetActive(false);
            viewerDialog.SetActive(false);
            messageDialog.SetActive(false);
            fullImage.texture = null;
            NoteImageStore.Release(viewerTexture);
            viewerTexture = null;
            dialogNoteId = expectedPhotoId = sourceChoice = null;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public bool TryCloseTopDialog()
        {
            if (!ready) return false;
            if (!sourceDialog.activeSelf && !replacementDialog.activeSelf && !removeDialog.activeSelf &&
                !viewerDialog.activeSelf && !messageDialog.activeSelf) return false;
            CloseDialogs();
            return true;
        }

        public void ShowMessage(string message)
        {
            if (messageText == null || messageDialog == null) { Debug.LogWarning(message); return; }
            messageText.text = message;
            messageDialog.SetActive(true);
        }

        private bool CanStart() => ready && pending == null && !recoveryError && app.SelectedNote != null && app.Store.LoadSucceeded;

        private void RefreshAvailability()
        {
            bool hasPhoto = NotePhotoData.Exists(app.SelectedNote?.photo);
            previewFrame.SetActive(hasPhoto && (!hidePreviewWhileTyping || !TouchScreenKeyboard.visible));
            section.SetActive(app.SelectedNote != null);
            addButton.interactable = CanStart();
            removeButton.gameObject.SetActive(hasPhoto);
            removeButton.interactable = CanStart() && hasPhoto;
            viewButton.interactable = hasPhoto;
            pendingPanel.SetActive(pending != null || recoveryError);
            retryButton.SetActive(awaitingRetry && pending != null);
        }

        private void RefreshSelected()
        {
            if (!ready) return;
            CloseDialogs();
            preview.texture = null;
            NoteImageStore.Release(previewTexture);
            previewTexture = null;
            bool hasPhoto = NotePhotoData.Exists(app.SelectedNote?.photo);
            missingImage.SetActive(hasPhoto);
            if (hasPhoto)
            {
                try
                {
                    previewTexture = images.Load(app.SelectedNote.photo.thumbnailPath);
                    preview.texture = previewTexture;
                    previewAspect.aspectRatio = (float)previewTexture.width / previewTexture.height;
                    missingImage.SetActive(false);
                }
                catch { /* Keep the editable missing-image placeholder and replacement controls. */ }
            }
            preview.enabled = previewTexture != null;
            RefreshAvailability();
        }

        public void CleanupFiles()
        {
            if (!ready || recoveryError || !app.Store.LoadSucceeded) return;
            try { images.Cleanup(app.Notes, app.Store.ReadBackupForCleanup(), pending); }
            catch (Exception exception) { Debug.LogWarning("Deferred picture cleanup: " + exception.Message); }
        }

        private void OnDestroy()
        {
            if (app != null) app.CurrentNoteChanged -= RefreshSelected;
            NoteImageStore.Release(previewTexture);
            NoteImageStore.Release(viewerTexture);
        }
    }

    [Serializable]
    internal sealed class NotePhotoResult
    {
        public string id;
        public string outcome;
        public string message;
        public string path;
        public bool pending;
    }

    internal static class AndroidNotePhotos
    {
        public static bool Begin(string source, int size, string id)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool accepted = false;
            WithBridge((bridge, activity) => accepted = bridge.CallStatic<bool>("beginRequest", activity, source, size, id));
            return accepted;
#else
            return false;
#endif
        }
        public static NotePhotoResult State()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string json = null;
            WithBridge((bridge, activity) => json = bridge.CallStatic<string>("state", activity));
            return JsonUtility.FromJson<NotePhotoResult>(json);
#else
            return null;
#endif
        }
        public static void Acknowledge(string id)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            WithBridge((bridge, activity) => bridge.CallStatic("acknowledge", activity, id));
#endif
        }
        public static void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            WithBridge((bridge, activity) => bridge.CallStatic("resetInterruptedRequest", activity));
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private static void WithBridge(Action<AndroidJavaClass, AndroidJavaObject> action)
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.manwell.photospike.PhotoBridge"))
                action(bridge, activity);
        }
#endif
    }
}
