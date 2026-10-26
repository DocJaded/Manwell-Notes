using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ManWellNotes.PhotoSpike
{
    // All visual objects and button events are saved in the test prefab.
    public sealed class PhotoSpikeController : MonoBehaviour
    {
        [Header("Saved UI references")]
        [SerializeField] private RawImage preview;
        [SerializeField] private AspectRatioFitter previewAspect;
        [SerializeField] private GameObject emptyHint;
        [SerializeField] private Text status;
        [SerializeField] private Button takePhotoButton;
        [SerializeField] private Button chooseImageButton;
        [SerializeField] private Button clearButton;
        [Header("Saved replacement confirmation")]
        [SerializeField] private GameObject replacementDialog;
        [SerializeField] private Button replacementNoButton;
        [Header("Import settings")]
        [SerializeField, Range(256, 2048)] private int maximumImageSize = 2048;
        [Header("Editor preview only")]
        [SerializeField] private Texture2D editorSample;

        [Serializable]
        private sealed class DeviceState
        {
            public string id;
            public string outcome;
            public string message;
            public string path;
            public string route;
            public int width;
            public int height;
            public bool pending;
        }

        private Texture2D ownedTexture;
        private string displayedPath;
        private string lastResult;
        private float nextPoll;
        private bool configured;
        private bool busy;
        private bool hasPicture;
        private string replacementSource;
        private GameObject previousSelection;

        private void Start()
        {
            configured = preview != null && previewAspect != null && emptyHint != null && status != null &&
                          takePhotoButton != null && chooseImageButton != null && clearButton != null &&
                          replacementDialog != null && replacementNoButton != null;
            if (!configured)
            {
                Debug.LogError("Photo test: assign all saved UI references in the Inspector.", this);
                enabled = false;
                return;
            }
            replacementDialog.SetActive(false);
#if UNITY_ANDROID && !UNITY_EDITOR && MANWELL_PHOTO_SPIKE
            PollDevice();
#else
            status.text = "Editor preview. Choose image uses the sample texture assigned in the Inspector. Test camera access in the Android APK.";
#endif
        }

        public void TakePhoto() => RequestImage("camera");
        public void ChooseImage() => RequestImage("picker");

        private void RequestImage(string source)
        {
            if (!configured || busy || replacementSource != null) return;
            if (!hasPicture) { Begin(source); return; }
            replacementSource = source;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            replacementDialog.SetActive(true);
            SetBusy(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(replacementNoButton.gameObject);
        }

        public void ConfirmReplacement()
        {
            if (!configured || replacementSource == null) return;
            string source = replacementSource;
            CloseReplacementDialog();
            // Approval opens the source; the existing file survives until a new import succeeds.
            Begin(source);
        }

        public void CancelReplacement()
        {
            if (!configured || replacementSource == null) return;
            CloseReplacementDialog();
        }

        private void CloseReplacementDialog()
        {
            replacementSource = null;
            replacementDialog.SetActive(false);
            SetBusy(busy);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }

        public void ResetInterruptedRequest()
        {
            if (!configured || replacementSource != null) return;
#if UNITY_ANDROID && !UNITY_EDITOR && MANWELL_PHOTO_SPIKE
            try { WithDevice((bridge, activity) => bridge.CallStatic("resetInterruptedRequest", activity)); }
            catch (Exception exception) { ShowError(exception); }
#else
            if (configured) status.text = "No Android request is running in the Editor.";
#endif
        }

        private void Begin(string source)
        {
            if (!configured || busy) return;
#if UNITY_ANDROID && !UNITY_EDITOR && MANWELL_PHOTO_SPIKE
            try
            {
                WithDevice((bridge, activity) => bridge.CallStatic("begin", activity, source, maximumImageSize));
                SetBusy(true);
                status.text = source == "camera" ? "Opening camera..." : "Opening image chooser...";
                lastResult = null;
            }
            catch (Exception exception) { ShowError(exception); }
#else
            if (source == "camera")
                status.text = "Camera access is tested on Android. The Editor does not simulate a camera result.";
            else if (editorSample != null)
            {
                ReleaseTexture();
                ShowTexture(editorSample);
                status.text = "Editor sample only. This does not test image selection or saving on Android.";
            }
            else status.text = "Assign an Editor Sample texture on the Photo Test controller, then try again.";
#endif
        }

        public void ClearImage()
        {
            if (!configured || busy || replacementSource != null) return;
#if UNITY_ANDROID && !UNITY_EDITOR && MANWELL_PHOTO_SPIKE
            try
            {
                WithDevice((bridge, activity) => bridge.CallStatic("clear", activity));
                lastResult = null;
                PollDevice();
            }
            catch (Exception exception) { ShowError(exception); }
#else
            ReleaseTexture();
            preview.texture = null;
            preview.enabled = false;
            emptyHint.SetActive(true);
            hasPicture = false;
            status.text = "Preview cleared.";
#endif
        }

        private void Update()
        {
            if (replacementSource != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelReplacement();
#if UNITY_ANDROID && !UNITY_EDITOR && MANWELL_PHOTO_SPIKE
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.5f;
            PollDevice();
#endif
        }

        private void OnApplicationFocus(bool focused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && MANWELL_PHOTO_SPIKE
            if (focused && configured) PollDevice();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && MANWELL_PHOTO_SPIKE
        private static void WithDevice(Action<AndroidJavaClass, AndroidJavaObject> action)
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.manwell.photospike.PhotoBridge"))
                action(bridge, activity);
        }

        private void PollDevice()
        {
            try
            {
                string json = null;
                WithDevice((bridge, activity) => json = bridge.CallStatic<string>("state", activity));
                if (json == lastResult) return;
                DeviceState state = JsonUtility.FromJson<DeviceState>(json);
                if (state == null) throw new IOException("The camera service returned no state.");
                hasPicture = !string.IsNullOrEmpty(state.path);
                SetBusy(state.pending);
                if (state.path != displayedPath)
                {
                    if (string.IsNullOrEmpty(state.path))
                    {
                        ReleaseTexture();
                        preview.texture = null;
                        preview.enabled = false;
                        emptyHint.SetActive(true);
                    }
                    else
                    {
                        // Native import already bounds decoded dimensions and writes a PNG.
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        try
                        {
                            if (!texture.LoadImage(File.ReadAllBytes(state.path), true))
                                throw new IOException("The saved test image could not be opened.");
                        }
                        catch { Destroy(texture); throw; }
                        ReleaseTexture();
                        ownedTexture = texture;
                        ShowTexture(texture);
                    }
                    displayedPath = state.path;
                }
                status.text = state.pending ? "Waiting for camera or image chooser..." : state.message;
                if (!state.pending && !string.IsNullOrEmpty(state.path))
                    status.text += $"\n{state.width} x {state.height} pixels | {state.route}\nClose and reopen this test app to check that the image stays saved.";
                lastResult = json;
            }
            catch (Exception exception) { ShowError(exception); }
        }
#endif

        private void ShowTexture(Texture texture)
        {
            preview.texture = texture;
            preview.enabled = true;
            emptyHint.SetActive(false);
            hasPicture = true;
            // The fitter mode and viewport geometry remain authored in the prefab.
            previewAspect.aspectRatio = (float)texture.width / texture.height;
        }

        private void SetBusy(bool busy)
        {
            this.busy = busy;
            takePhotoButton.interactable = chooseImageButton.interactable = clearButton.interactable =
                !busy && !replacementDialog.activeSelf;
        }

        private void ShowError(Exception exception)
        {
            SetBusy(false);
            status.text = "Photo test could not complete. " + exception.Message;
            Debug.LogException(exception, this);
        }

        private void ReleaseTexture()
        {
            if (ownedTexture != null) Destroy(ownedTexture);
            ownedTexture = null;
        }

        private void OnDestroy() => ReleaseTexture();
    }
}
