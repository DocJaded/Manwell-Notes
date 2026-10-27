using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ManWellNotes
{
    public sealed class NoteAudioController : MonoBehaviour
    {
        [SerializeField] private NotesApp app;
        [SerializeField] private NotePhotoController messages;
        [SerializeField] private Button recordButton;
        [SerializeField] private GameObject section;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button stopPlaybackButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private GameObject[] banners;
        [SerializeField] private TMP_Text[] timerTexts;
        [SerializeField] private Button[] stopRecordingButtons;
        [SerializeField] private GameObject removeDialog;
        [SerializeField] private Button[] retryButtons;
        [SerializeField, Range(1, 720)] private int maximumRecordingMinutes = 120;

        private NoteAudioStore files;
        private AudioState state = new AudioState();
        private bool initialized, requestingPermission, starting, saveFailed, stateKnown, statusErrorShown;
        private bool beginWhenFocused;
        private float nextPoll, startDeadline;
        private string removeNoteId, removeAudioId, lastPlaybackError;
#if UNITY_ANDROID
        private PermissionCallbacks permissionCallbacks;
#endif
        public bool Protects(string noteId) => (initialized && !stateKnown) || starting || (state.noteId == noteId && (state.Busy || state.phase == "ready"));

        private void Start()
        {
            UnityEngine.Object[] required = { app, messages, recordButton, section, statusText, playButton,
                stopPlaybackButton, removeButton, removeDialog };
            foreach (var reference in required)
                if (reference == null) { Debug.LogError("Assign the saved recording interface references.", this); enabled = false; return; }
            if (banners == null || banners.Length != 3 || timerTexts.Length != 3 || stopRecordingButtons.Length != 3 || retryButtons.Length != 3)
            { Debug.LogError("Assign a recording status strip for each saved page.", this); enabled = false; return; }
            files = new NoteAudioStore(Application.persistentDataPath);
            app.CurrentNoteChanged += OnNoteChanged;
            initialized = true;
            Poll();
            Refresh();
            CleanupFiles();
        }

        private void Update()
        {
            if (!initialized) return;
            if (beginWhenFocused && Application.isFocused)
            {
                beginWhenFocused = false;
                requestingPermission = false;
                BeginRecording();
            }
            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + 0.25f;
                Poll();
                Refresh();
            }
        }

        public void RecordSound()
        {
            if (!initialized || !stateKnown || starting || requestingPermission || !string.IsNullOrEmpty(state.phase) || !app.Store.LoadSucceeded) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            requestingPermission = true;
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                permissionCallbacks = new PermissionCallbacks();
                permissionCallbacks.PermissionGranted += _ => RequestNotifications();
                permissionCallbacks.PermissionDenied += _ => PermissionRejected();
                permissionCallbacks.PermissionDeniedAndDontAskAgain += _ => PermissionRejected();
                permissionCallbacks.PermissionRequestDismissed += _ => PermissionRejected();
                Permission.RequestUserPermission(Permission.Microphone, permissionCallbacks);
            }
            else RequestNotifications();
#else
            messages.ShowMessage("Sound recording is available in the Android preview app.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void RequestNotifications()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                const string notificationPermission = "android.permission.POST_NOTIFICATIONS";
                if (version.GetStatic<int>("SDK_INT") >= 33 && !Permission.HasUserAuthorizedPermission(notificationPermission))
                {
                    permissionCallbacks = new PermissionCallbacks();
                    permissionCallbacks.PermissionGranted += _ => beginWhenFocused = true;
                    permissionCallbacks.PermissionDenied += _ => beginWhenFocused = true;
                    permissionCallbacks.PermissionDeniedAndDontAskAgain += _ => beginWhenFocused = true;
                    permissionCallbacks.PermissionRequestDismissed += _ => beginWhenFocused = true;
                    Permission.RequestUserPermission(notificationPermission, permissionCallbacks);
                    return;
                }
            }
            beginWhenFocused = true;
        }
        private void PermissionRejected()
        {
            requestingPermission = false;
            messages.ShowMessage("Microphone permission is needed to record. You can enable it in Android app settings. No recording note was created.");
        }
#endif

        private void BeginRecording()
        {
            if (starting || state.Busy || !string.IsNullOrEmpty(state.phase)) return;
            try
            {
                StopPlayback();
                string timestamp = NoteAudioStore.Timestamp(DateTime.Now);
                var note = app.CreateRecordingNote(timestamp);
                if (note == null) { messages.ShowMessage(app.Store.LastError); return; }
                string relative = NoteAudioStore.RelativePath(note.id, timestamp);
                files.Resolve(relative);
                starting = true;
                startDeadline = Time.unscaledTime + 35;
                NativeAudio.Call("begin", Application.persistentDataPath, note.id, Guid.NewGuid().ToString("N"), relative, maximumRecordingMinutes);
            }
            catch (Exception e) { starting = false; messages.ShowMessage("Recording could not start. " + e.Message); }
            Refresh();
        }

        private void Poll()
        {
            try
            {
                state = NativeAudio.State();
                stateKnown = true;
                if (!string.IsNullOrEmpty(state.phase)) starting = false;
                if (starting && Time.unscaledTime > startDeadline)
                { starting = false; messages.ShowMessage("The recorder did not start. Your new note was kept; please try again."); }
                if (state.phase == "ready" && !saveFailed) AttachCompleted();
                else if (state.phase == "error")
                {
                    string message = state.message;
                    NativeAudio.Call("acknowledge", state.id);
                    state.phase = "";
                    messages.ShowMessage(message);
                }
                if (!string.IsNullOrEmpty(state.playbackError) && state.playbackError != lastPlaybackError)
                    messages.ShowMessage(state.playbackError);
                lastPlaybackError = state.playbackError;
                statusErrorShown = false;
            }
            catch (Exception e)
            {
                if (!statusErrorShown) messages.ShowMessage("Recording status is unavailable. " + e.Message);
                statusErrorShown = true;
                stateKnown = false; // Do not start another recording or delete unknown pending files.
            }
        }

        private void AttachCompleted()
        {
            var note = app.FindNote(state.noteId);
            if (note != null && note.audio?.id != state.id)
            {
                bool valid;
                try { valid = state.path.StartsWith("audio/" + note.id + "/", StringComparison.Ordinal) && File.Exists(files.Resolve(state.path)); }
                catch { valid = false; }
                if (!valid)
                {
                    NativeAudio.Call("acknowledge", state.id);
                    state.phase = "";
                    messages.ShowMessage("The recorded file is unavailable. Your note and its text were kept.");
                    return;
                }
                if (NoteAudioData.Exists(note.audio) || !app.TrySetAudio(note.id, "", new NoteAudioData { id = state.id, path = state.path, durationMs = state.durationMs }))
                {
                    saveFailed = true;
                    messages.ShowMessage("The sound file was kept, but its note could not be saved. Free storage if needed, then use Retry save.");
                    return;
                }
            }
            NativeAudio.Call("acknowledge", state.id);
            string message = state.message;
            state.phase = "";
            saveFailed = false;
            CleanupFiles();
            if (!string.IsNullOrEmpty(message)) messages.ShowMessage(message);
        }

        public void RetrySave() { saveFailed = false; Poll(); Refresh(); }
        public void StopRecording() { try { NativeAudio.Call("stop"); } catch (Exception e) { messages.ShowMessage(e.Message); } }
        public void Play()
        {
            if (!NoteAudioData.Exists(app.SelectedNote?.audio) || state.Busy || starting) return;
            try { files.Resolve(app.SelectedNote.audio.path); NativeAudio.Call("play", Application.persistentDataPath, app.SelectedNote.audio.path); }
            catch (Exception e) { messages.ShowMessage(e.Message); }
        }
        public void StopPlayback() { try { NativeAudio.Call("stopPlayback"); } catch (Exception e) { Debug.LogWarning(e.Message); } }
        public void AskRemove()
        {
            if (!NoteAudioData.Exists(app.SelectedNote?.audio) || Protects(app.SelectedNote.id)) return;
            removeNoteId = app.SelectedNote.id;
            removeAudioId = app.SelectedNote.audio.id;
            removeDialog.SetActive(true);
        }
        public void ConfirmRemove()
        {
            if (!removeDialog.activeSelf) return;
            StopPlayback();
            bool saved = app.TrySetAudio(removeNoteId, removeAudioId, null);
            CloseDialog();
            if (!saved) messages.ShowMessage("The recording could not be removed. Your saved audio was kept.");
            else CleanupFiles();
        }
        public void CloseDialog()
        {
            if (removeDialog != null) removeDialog.SetActive(false);
            removeNoteId = removeAudioId = null;
            StopPlayback();
        }
        public bool TryCloseDialog()
        {
            if (removeDialog == null || !removeDialog.activeSelf) return false;
            CloseDialog();
            return true;
        }
        private void OnNoteChanged() { CloseDialog(); if (initialized) Refresh(); }
        private static string Duration(long ms) => TimeSpan.FromMilliseconds(Math.Max(0, ms)).ToString(@"hh\:mm\:ss");
        private void Refresh()
        {
            if (!initialized) return;
            bool busy = starting || state.Busy;
            bool attached = NoteAudioData.Exists(app.SelectedNote?.audio);
            section.SetActive(attached);
            statusText.text = attached ? "Sound · " + Duration(app.SelectedNote.audio.durationMs) : "";
            recordButton.interactable = stateKnown && !busy && !requestingPermission && string.IsNullOrEmpty(state.phase) && app.Store.LoadSucceeded;
            playButton.interactable = attached && !busy && !state.playing;
            stopPlaybackButton.interactable = state.playing;
            removeButton.interactable = attached && !busy;
            for (int i = 0; i < banners.Length; i++)
            {
                banners[i].SetActive(busy || saveFailed);
                timerTexts[i].text = saveFailed ? "Retry note save" : state.phase == "recording" ? "Recording " + Duration(state.durationMs) : "Preparing…";
                stopRecordingButtons[i].gameObject.SetActive(busy);
                stopRecordingButtons[i].interactable = state.phase == "recording";
                retryButtons[i].gameObject.SetActive(saveFailed);
            }
        }
        public void CleanupFiles()
        {
            if (!initialized || !stateKnown || !app.Store.LoadSucceeded || starting) return;
            try { files.Cleanup(app.Notes, app.Store.ReadBackupForCleanup(), string.IsNullOrEmpty(state.phase) ? null : state.path); }
            catch (Exception e) { Debug.LogWarning("Deferred audio cleanup: " + e.Message); }
        }
        private void OnApplicationPause(bool paused) { if (paused) StopPlayback(); }
        private void OnDestroy() { if (app != null) app.CurrentNoteChanged -= OnNoteChanged; StopPlayback(); }
    }

    [Serializable]
    public sealed class AudioState
    {
        public string id, noteId, phase, path, message, playbackError;
        public long durationMs;
        public bool playing;
        public bool Busy => phase == "starting" || phase == "recording" || phase == "stopping";
    }
    internal static class NativeAudio
    {
        public static void Call(string method, params object[] arguments)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.manwell.photospike.NoteAudioBridge"))
            {
                var args = new object[arguments.Length + 1];
                args[0] = activity;
                Array.Copy(arguments, 0, args, 1, arguments.Length);
                bridge.CallStatic(method, args);
            }
#endif
        }
        public static AudioState State()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.manwell.photospike.NoteAudioBridge"))
                return JsonUtility.FromJson<AudioState>(bridge.CallStatic<string>("state", activity));
#else
            return new AudioState();
#endif
        }
    }
}
