# Photos in notes

Normal-app update: these features are now included in version 0.2.0. See APP_UPDATE_0.2.0.md for the normal APK and update instructions. The separate preview remains available.

Updated 27 October 2026. The user confirmed the integrated photos and enlarged-viewer pinch zoom work on the phone. The same preview now includes the first sound-recording implementation; see SOUND_RECORDING_GUIDE.md for that feature and its pending phone tests.

## Safe preview build

Install `Release_APK/NotesPhotosPreview.apk` and open **Notes Photos Preview**. Its ID is `com.manwell.notes.photospreview`, separate from both the normal notes app and the original photo test. It starts with its own empty notebook and does not replace or read your normal notes installation. No installation is performed automatically.

The preview uses the real notes scene, with one picture per note. Tap **Add picture**, then **Take photo** or **Choose image**. The accepted image saves automatically. A picture appears below the text; tap it to enlarge it. Removing a picture requires confirmation. Replacing one shows **The existing picture will be replaced**, with **Yes / No**. Cancelling or a failed save keeps the current picture.

Android input remains JPEG or non-animated PNG. Videos and other image formats are not supported in this version. Images are resized to at most 2048 pixels along the longest edge and stored as PNG, with a smaller thumbnail. There is no clipboard paste or inline image editing.

In the enlarged viewer, pinch with two fingers to zoom between fit-to-screen and 4×. Drag with one finger while zoomed in to inspect other parts of the picture. Panning is bounded and the picture is clipped to the viewer area so the Close button remains available. Closing and reopening resets zoom and position. The thumbnail is unchanged. Zoom magnifies the stored picture; it does not recover detail removed during resizing.

## Edit normally in Unity

- Open `Assets/Scenes/SampleScene.unity`.
- Edit `Assets/ManWellNotes/Prefabs/Note Picture.prefab` for the buttons, thumbnail and pending-request controls below the text.
- Edit `Assets/ManWellNotes/Prefabs/Picture Dialogs.prefab` for source selection, replacement/removal confirmations, enlarged viewer and error messages. Dialogs are saved inactive; activate temporarily to edit them and restore the inactive state before saving.
- The scene's **NotePhotoController** references these saved objects. Its Inspector exposes maximum image size, thumbnail size and whether the preview hides while the keyboard is open.
- In **Picture Dialogs > Viewer > Viewport**, **NotePhotoZoom > Maximum Zoom** controls the zoom limit. The saved Rect Mask 2D clips the enlarged image. Keep the viewport/image references assigned; runtime changes to image scale and position are only the user's viewing gesture.
- Button actions are saved Inspector events on the scene's prefab instances. Layout, fonts, colors, spacing and labels remain editable. No photo interface objects are created at runtime.
- Keep the **Picture Dialogs** parent enabled. Only its individual Source, Replacement, Remove, Viewer and Message children should start disabled. Keep **Note Picture > Actions** enabled; the controller shows/hides the overall picture section. Disabling either parent can hide otherwise working buttons/dialogs. **Tools > Man-Well Notes > Validate Photo and Sound Visibility** checks these saved states and the Editor button responses without reading your notes.
- The body field's saved minimum height is reduced to make room for the picture. Title and navigation minimum heights are preserved. Subsequent setup runs do not reapply these adjustments.

Use **Tools > Man-Well Notes > Photos** for validation, storage checks and builds. **Add Missing Photo Interface** creates absent assets and wiring; it preserves existing prefab styling and avoids duplicate instances. The older full interface rebuild command refuses to discard an integrated photo interface.

**Build Separate Photos Preview APK** temporarily changes the app identity for the separate preview and restores project settings afterward. **Build Notes With Photos APK** uses the normal `com.manwell.notes` identity: installing that build would update the normal app if its signing certificate matches. Use the separate preview until integration testing is complete. Both menu commands currently create development APKs; release packaging is a later step.

## Saving and recovery

Photo files live in private per-note folders beside the notes JSON. Each note stores relative image and thumbnail paths. Existing text-only notes remain readable.

The original note ID and picture ID are saved before opening the camera or picker. A delayed result is attached only to that original note, even if selection changes. Deleted notes and changed pictures reject stale results. Restart recovery recognizes an already saved result without attaching it twice.

New image files are completed before the note reference changes. JSON saves use a temporary file and replacement with a previous-save backup. A failed photo save restores the previous in-memory reference. Unreadable notes and backups block saving rather than replacing the notebook with an empty one.

Cleanup preserves images referenced by current notes, the previous-save backup, and an outstanding photo request. Consequently, removed or replaced images may remain privately stored until the backup advances. Unknown backup contents defer cleanup. Gallery originals are never deleted.

The shared Android bridge remains under `Assets/ManWellNotes/PhotoSpike/Plugins/Android`. It is now enabled for the integrated app as well as the standalone test. There is one native library, not a second copy.

## Verification and next phone tests

Automated checks cover old note data, image/thumbnail files, path validation, delayed results, save failure rollback, replacement, removal, stale/deleted notes, restart idempotency, backup recovery and cleanup. They use temporary fixtures, not your real notes.

Verified on 26 October: 27 Unity storage/recovery checks and seven native import boundary checks passed. Saved UI references and button events validated; running setup twice produced no duplicate photo controls. The ARM64 Android preview build and APK signature verification passed (minimum Android 8/API 26). The editor and replacement-dialog previews were visually inspected. The user subsequently confirmed the integration works as specified on 27 October.

The zoom addition passes three further checks covering off-center pinch anchoring, midpoint panning and bounds, bringing the Unity check total to 30. Actual two-finger gestures still need phone verification.

On the separate preview app:

The base integration checklist below was reported working as specified on 27 October. For the new zoom build, test off-center pinching, one-finger panning, minimum/maximum zoom, portrait/landscape images, and Close/Android Back followed by reopening. Verify the thumbnail stays unchanged and Close remains usable at maximum zoom.

1. Create two notes, attach a different picture to each, switch between them, then restart. Each note must retain its own text and picture.
2. Repeat the three replacement scenarios already passed in the isolated test: No; Yes then cancel; Yes then accept and restart.
3. Remove one picture, restart, and verify the other note remains unchanged. Delete a note with a picture and verify the remaining note.
4. Check a long note with the keyboard open, portrait/landscape photos, tap-to-enlarge, and Android Back from each photo dialog.
5. Background/reopen while a source is open. If a request is interrupted, verify Cancel request unlocks adding photos without losing an existing saved picture.

The Unity Editor can choose a PNG up to 2048 pixels per edge for interface checks. Native camera/gallery behavior, Android file replacement and interruption recovery still require device verification.
