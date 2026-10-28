# Silent sound recording preview

Normal-app update: the user approved incorporating this feature into version 0.2.0. See APP_UPDATE_0.2.0.md for the normal APK. The preview workflow below remains available for future tests.

27 October 2026. Implemented for testing in the existing **Notes Photos Preview** app (`com.manwell.notes.photospreview`). This is the same separate preview identity used for photos and pinch zoom. Install the updated APK over that preview; do not uninstall it if you want to retain its test notes. The normal notes installation is separate.

## Recording flow

- Tap **RS**, next to **New note** on the Library page.
- On first use, grant microphone permission. Android 13+ also asks about notifications so the recording's Stop control can appear outside the app. Declining microphone permission creates no note. Notification permission is optional for recording, but declining it can hide the notification drawer control.
- A new normal note is saved with local-time title `YYYY_MM_DD_HH_MM` and an empty body. Recording begins without app-generated sound or vibration. The title and body remain editable; changing the title does not rename the audio file.
- Record with the screen locked or switch apps. A microphone foreground service owns recording independently of Unity's updates. Recording status and Stop controls are saved into each of the app's three pages, and a quiet Android notification also has Stop.
- Tap **Stop** in the app or notification. Completed sound is attached to the original new note even if you selected another note. Playback and Remove controls appear below the note text/photo section. Removing sound requires Yes/No confirmation and keeps the note's text.
- Each RS press starts a new recording note. It does not replace or append audio in an existing note. Only one capture runs at a time. A note with an active or unsaved completed recording cannot be deleted until it is stopped and saved.
- Playback is explicit, uses the device's media volume and stops when changing notes/pages, backgrounding the app, or losing audio focus. It does not play through the speaker during recording.

The file is AAC audio in an M4A container, named for example `2026_10_27_09_05.m4a`. Each recording note has a unique folder (`audio/<note-id>/`), so two recordings within the same minute have the requested filename without colliding. Default capture settings are mono, 44.1 kHz and 96 kbps. The Inspector duration limit defaults to 120 minutes (adjustable from 1–720); a 256 MB native file limit also stops and saves capture.

## Unity editing

Open `Assets/Scenes/SampleScene.unity`. The app root has **NoteAudioController** with saved references and **Maximum Recording Minutes**.

Editable prefabs in `Assets/ManWellNotes/Prefabs/`:

- **Record Sound**: RS action beside the existing New note button. The RS label is a scene override; replace its saved text or image with an icon normally in Unity.
- **Recording Status**: timer, Stop and retry-save controls, with an instance on each page. Layout groups reserve space when a strip is visible.
- **Note Sound**: duration, Play, Stop and Remove controls.
- **Sound Dialogs**: removal confirmation.

Keep the **Sound Dialogs** parent enabled and its **Remove** child disabled initially. The shared **Picture Dialogs** parent must also stay enabled so messages can appear. In Unity Editor Play Mode, RS displays an Android-only explanation; it does not capture audio from the computer microphone.

Button events are saved in the scene. Runtime code changes content, visibility and availability; it creates no recording UI. **Tools > Man-Well Notes > Sound > Add Missing Recording Interface** creates absent assets/wiring and preserves existing edits. It can run twice without adding duplicates. **Validate Recording Interface** checks references and events; **Run Recording Storage Checks** tests note/file behavior in temporary fixtures.

The existing **Photos > Build Separate Photos Preview APK** menu builds the saved integrated scene, including sound, into `Release_APK/NotesPhotosPreview.apk`. Android must be the active platform. The native recorder and playback bridge are in the existing shared Android library under `PhotoSpike/Plugins/Android`; no new third-party package is required.

## Persistence and interruption

The note is saved before starting capture. The native request stores its original note ID, unique request ID, path and state. Finishing while Unity is paused leaves a durable result to attach on return. A completed result can be replayed after a restart without creating a second note or attachment. Failed JSON writes keep the sound file and expose Retry save.

The app does not resume microphone capture automatically after a killed process or reboot. It checks whether an interrupted file has usable finalized audio and attaches that if available; otherwise it reports the failure and keeps the new note and any text you entered. Abrupt force-stop/power loss may leave an unplayable M4A file. Very short recordings or microphone/storage failures may produce no playable audio.

Cleanup retains audio referenced by current notes, the previous-save backup or an outstanding request. Removal/deletion may therefore leave the private audio file until the backup advances and cleanup next runs. App uninstall or clearing app data removes its private notes and media.

## Device test checklist

Build verification: 19 recording storage checks and the existing 30 photo/zoom checks passed. Saved audio references and events validated, including running setup twice without duplicates. Native Java compiled, the Android preview built successfully, and its signature, separate app ID, microphone service type and permission declarations were verified. The Library, recording and saved-audio layouts were visually inspected. No device recording test has been performed by the assistant.

1. Grant permissions, tap RS, speak for 10 seconds and Stop. Confirm no initial app sound, a timestamp title, blank body, correct playback and matching filename format.
2. Edit title/body, save and restart. Confirm text and audio persist. Create two recordings in the same minute and verify both play independently.
3. Record while locked for a minute, unlock and Stop. Repeat by stopping from the notification while locked, then reopen the app and play the whole recording.
4. Switch to another note while recording. Confirm the finished audio belongs to the original recording note. Confirm active recording notes cannot be deleted prematurely.
5. Decline microphone permission, then grant it through Android settings and retry. Check notification denial and dismissal do not leave RS stuck.
6. Remove audio with No/Yes, restart, and delete a recording note. Verify unrelated notes/photos/audio remain intact.
7. Check interruptions: another app using the microphone, an incoming call, app process termination and low storage. Check playback after returning; do not assume an interrupted file is complete merely because a timer advanced.
8. Confirm existing photo selection, camera, replacement and pinch zoom still work.

Native capture, lock-screen behavior, OEM microphone handling and the absence of a device-generated sound require real-phone verification. The app itself generates no start beep and creates its notification channel with no sound or vibration. Android microphone privacy indicators remain in place.

Platform references: [Android MediaRecorder](https://developer.android.com/media/platform/mediarecorder), [microphone foreground services](https://developer.android.com/develop/background-work/services/fgs/service-types#microphone), [notification permission](https://developer.android.com/develop/ui/compose/notifications/notification-permission), and [Unity Android permissions](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/android/developing/device-features-and-permissions/permissions-in-unity).
