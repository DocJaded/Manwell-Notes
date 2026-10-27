# Man-Well Notes 0.2.0

This update brings the tested preview features into the normal app: one photo per note, camera/gallery selection, replacement confirmation, enlarged viewing with pinch zoom, and RS sound recording that creates a timestamped editable note.

Verification: the normal-app build succeeded, all 49 automated checks passed, and the APK signature verified. Its application ID is `com.manwell.notes`, version code is `2`, and it has no Android debuggable flag. Its signing certificate SHA-256 matches the previous normal APK: `889e3dc4a151fb9f9430b969a1da8a158e6bdba10d30628fe0464d4391f762e4`. The APK has not been installed on your phone by the assistant.

## Install the normal app update

Install `Release_APK/ManWellNotes-0.2.0.apk` over the existing **Man-Well Notes** installation. Do not uninstall the app or clear its storage. The update uses the normal `com.manwell.notes` identity, version name `0.2.0`, version code `2`, and the existing signing certificate. Existing notes remain in the same data folder, and text-only note files are supported by the new storage code.

The signing certificate is compared against the previous `Release_APK/ManWellNotes.apk` in this workspace. If the phone has a differently signed installation, Android will reject the update; do not uninstall to work around that rejection if you want to preserve its notes.

**Notes Photos Preview** remains a separate app with separate data. This update does not copy preview notes, photos or recordings into the normal app. The original APK and the preview APK remain available in `Release_APK`.

On first use of RS in the normal app, grant microphone permission and, when asked, notification permission for the outside-app Stop control. Permissions granted to the preview do not transfer to the normal app.

## Unity workflow

The integrated interface is saved in `Assets/Scenes/SampleScene.unity`. All photo, recording and confirmation controls remain editable scene/prefab objects. Use **Tools > Man-Well Notes > Build Updated Notes APK** to validate the saved interface, run the 49 storage/zoom checks, and build a normal-app APK named for the configured version. This command builds without Unity development/debug options and preserves the configured signing identity.

The existing separate-preview build command remains available for future experiments. It temporarily changes app identity and restores settings afterward.

The existing installation was signed with the machine's Android debug certificate. That certificate is intentionally retained for update compatibility. This is a direct-install app update, not a Play Store signing/publication setup.

Editing and feature details: [Photo guide](PHOTO_INTEGRATION_GUIDE.md), [Sound recording guide](SOUND_RECORDING_GUIDE.md).

After installing, check that existing notes are present, then try a photo and a short recording in the normal app. Keep the preview until any test material you want to retain is no longer needed there.
