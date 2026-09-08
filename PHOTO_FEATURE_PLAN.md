# Note photos — implementation plan

Status: Planning only. No feature implementation has been made.
Date: 8 September 2026

## 1. Agreed scope

Add still images below a note's text. The user taps **Add photo**, then chooses **Take photo** or **Choose image**. An accepted camera shot or selected image is copied into the app's private storage and saved with the current note automatically.

Support multiple images in insertion order, thumbnail previews, tap to enlarge, and removal with confirmation. The original gallery image remains untouched. Cancelling the camera or picker returns to the same note without adding anything.

First target: the existing Android app. Unity Editor support is for previewing and testing the interface with sample images; desktop camera integration is outside this first version.

Excluded: videos, clipboard image paste, inline images between paragraphs, rich text editing, photo editing, image reordering, cloud sync, and export. Camera photos need not also be published to the device gallery.

## 2. Unity workflow requirement

**The saved scene and prefabs are the source of truth for appearance and layout.** The owner must be able to change the interface in Unity without editing scripts.

- Create and save buttons, panels, layouts, labels, image placeholders, and dialogs in the Editor.
- Expose references through serialized Inspector fields. Wire fixed button actions through Inspector events where practical; avoid also registering the same action in code.
- Use standard RectTransform, LayoutElement, layout groups, ScrollRect, RawImage, Button, and TextMesh Pro components.
- Store dimensions, spacing, colors, fonts, anchors, and thumbnail presentation in scenes/prefabs. Do not overwrite them in Awake, Start, or refresh methods.
- Instantiate the saved photo-card prefab for each attached image, just as the existing app instantiates its note-card prefab. Runtime code supplies the image and its associated data; it does not construct the card's visual children.
- Save the source-choice dialog, enlarged-photo viewer, and removal dialog in the scene, initially inactive. Runtime code may show and hide them.
- Missing references should produce an actionable Editor validation error, not silently generate replacement UI during play.
- An optional one-time Editor setup command may create missing assets and assign references. It must use Undo, preserve existing assets and manual edits, and never run automatically on startup or on entering Play Mode.

Runtime code is still necessary for device integration, reading and writing images, creating the appropriate number of prefab instances, and responding to user actions. Minimize runtime UI construction, rather than trying to eliminate necessary application behavior.

## 3. Current project integration points

| Existing asset | Planned responsibility |
| --- | --- |
| `Assets/Scenes/SampleScene.unity` | Save the photo section and dialogs in the existing editor page. |
| `Assets/ManWellNotes/Scripts/NotesApp.cs` | Connect note selection, text saving, photo requests, modification dates, and note deletion. |
| `Assets/ManWellNotes/Scripts/NoteData.cs` | Add attachment metadata and update NoteStore persistence behavior. NoteStore is defined in this file. |
| `Assets/ManWellNotes/Scripts/NotesPageNavigator.cs` | Close a photo dialog/viewer before navigating away when Android Back is pressed. |
| `Assets/ManWellNotes/Scripts/KeyboardSafeArea.cs` | Verify photo controls remain usable with the keyboard open. |
| `Assets/ManWellNotes/Prefabs/Note Card.prefab` | Existing example of editable prefab instances; leave note-list appearance unchanged for this feature. |
| `Assets/ManWellNotes/Editor/NotesSceneBuilder.cs` | Review its rebuild behavior so it does not become a second, conflicting definition of the photo UI. |

NotesApp currently applies some editor layout settings at startup through ConfigureEditorLayout and related methods. Audit the touched controls and move their fixed presentation values to the saved scene as needed. Do not broaden this into a full app rewrite, but ensure those methods cannot reset the new layout or its Inspector settings.

## 4. Saved UI assets

Proposed hierarchy within the existing editor page:

```text
Editor page
  Existing toolbar, title and date
  Existing text editor and scrollbar
  Photos section
    Add photo button
    Photo strip (horizontal ScrollRect)
      Viewport
        Content (HorizontalLayoutGroup)
          Photo Card instances

Dialog layer
  Photo source dialog
    Take photo / Choose image / Cancel
  Photo viewer
    Image / Close
  Remove photo confirmation
    Remove / Cancel
  Photo operation feedback
```

Use a compact horizontal photo strip below the existing text editor. This preserves the text editor's vertical scrolling and avoids introducing competing vertical scroll areas. Keep Add photo visible for notes without images; hide the empty strip. Verify this arrangement at the app's small portrait reference size before committing to dimensions.

Create `Assets/ManWellNotes/Prefabs/Photo Card.prefab` with a thumbnail RawImage, an open button, a separate remove button, and a saved placeholder for an unreadable image. Preserve aspect ratio without stretching. Keep the card geometry editable in the prefab.

Proposed supporting components:

- **NotePhotosController:** binds the current note's attachments to the saved UI, coordinates add/remove requests, and manages loading feedback.
- **PhotoCardView:** holds the prefab references and presents one attachment.
- **PhotoViewer:** displays a single image in the saved overlay and handles closing it.
- **NoteImageStore:** owns image files, thumbnails, import validation, and cleanup.
- **DevicePhotoService:** a narrow platform interface for camera and image selection, with an Android implementation and an Editor test implementation.

These are proposed names, not existing components. Keep business logic out of presentation components.

## 5. Android camera and stored-image selection

Use the device's existing camera application for capture. Do not build a live camera preview inside Unity.

The camera integration should supply a temporary output file through a content URI with appropriately scoped access, then consume the full-size result after acceptance. Do not rely on the small thumbnail that some camera results return. See Android's [common camera intents](https://developer.android.com/guide/components/intents-common).

For stored images, use the Android system Photo Picker restricted to images, one selection per request. Provide a document-picker fallback where the Photo Picker is unavailable, and copy the returned content into app storage promptly. Avoid broad gallery/storage access for this workflow. See Android's [Photo Picker documentation](https://developer.android.com/training/data-storage/shared/photo-picker).

Before selecting a Unity integration package or writing a small native bridge, verify support for the project's installed Unity version, IL2CPP/ARM64 build, minimum Android API 26, full-resolution output, URI handling, cancellation, and activity recreation. Record the dependency version and license if a package is used. No package is selected by this plan.

Review the merged Android manifest and request only permissions actually needed by the chosen integration. Handle unavailable camera applications and denied access gracefully. Prefer the native capture handoff so the app does not need an embedded-camera permission flow.

Persist the initiating note ID and pending operation before leaving Unity. Save the current text first and stop the request if saving fails. Prevent duplicate launches. On return, resolve the original note ID rather than attaching to whichever note happens to be selected. If the note no longer exists, discard the pending import safely.

Account for Android suspending or recreating the app while the camera is open. Recover a completed pending capture where possible; otherwise cancel cleanly and clear temporary files. Never silently attach a photo to a different note.

## 6. Data and file storage

Add an ordered attachment list to NoteData. Each record contains a stable attachment ID, relative image path, relative thumbnail path, and image dimensions. Treat a missing or null list in existing JSON as an empty list so older notes continue to load.

Suggested private storage:

```text
Application.persistentDataPath/
  man-well-notes.json
  images/
    <note-id>/
      <attachment-id>.jpg or .png
      <attachment-id>-thumb.jpg or .png
  pending-photo.json
  pending-images/
```

Use generated IDs for paths, not note titles or source filenames. Store relative paths in JSON and validate that resolved file paths stay inside the managed image directory. Do not store image bytes in the JSON or retain external gallery paths as the attachment's permanent source.

Proposed initial import settings, exposed in the Inspector:

| Setting | Starting value |
| --- | --- |
| Maximum saved image long edge | 2048 pixels, never upscale |
| Thumbnail long edge | 320 pixels |
| JPEG quality | 85 |
| Images selected per request | 1 |

Validate these defaults on a representative phone before release. Downsample before allocating a full-resolution texture where possible, apply orientation before re-encoding, and preserve transparency using PNG when required. Define source-byte and decoded-pixel safety limits during the device spike and expose them as settings. Handle unsupported or corrupt files with a short user-facing message. Validate decoded content as well as picker MIME type; reject videos and animated image formats in this first version. Normalize supported phone formats to JPEG/PNG; do not promise HEIC support until tested with the chosen decoder.

## 7. Save reliability, removal, and memory

NoteStore.Save currently catches errors and returns no success result. Adjust that contract as part of implementation so photo operations can distinguish a successful save from a failed write.

Import transaction:

1. Capture/select into temporary storage and validate the result.
2. Write the normalized image and thumbnail completely.
3. Add the attachment metadata and update the note's modified timestamp.
4. Persist notes using a temporary JSON file and a platform-tested replacement/recovery strategy.
5. Present the attachment as saved only after persistence succeeds. On failure, roll back metadata and clean up unreferenced imported files; keep the note text intact.

For attachment or note deletion, persist the metadata removal successfully before deleting its image files. Restore in-memory state if persistence fails. If file deletion fails after the metadata save, leave cleanup for a later safe pass. Only remove files in the app-managed image directory that are demonstrably unreferenced; exclude pending operations. Never run orphan cleanup after a failed JSON load that could otherwise look like an empty notebook.

Load thumbnails for the current note and load the larger image only while the viewer needs it. Release owned runtime textures when cards or viewer content are replaced. Do not load every note's full-size images at launch. A missing file should show a placeholder without breaking text editing.

## 8. Implementation order

1. **Device integration spike:** verify camera acceptance/cancellation, full-size output, image picker/fallback, orientation, and app recreation on Android. Select and document the native integration. Keep this spike isolated from the production scene.
2. **Editor layout:** create the saved section, prefab, and dialogs. Assign references and preview using sample images. Verify text editing, scrolling, and keyboard space. Confirm Inspector changes survive entering Play Mode.
3. **Persistence:** introduce backward-compatible attachment data, file import, pending-operation recovery, save results, and safe removal.
4. **Connect behavior:** wire the saved buttons, import callbacks, thumbnail population, viewer, note switching, and Android Back handling.
5. **Validate and hand over:** run the checks below, create an Android build, and document which scene objects and prefab fields the owner can edit.

Do not rebuild the entire existing interface as a shortcut. If the old rebuild command cannot preserve the new authored UI, make its behavior explicit and protect against accidental loss of the photo section. Prefer an explicit, non-destructive Editor setup/validation action over repeated generation.

## 9. Acceptance checks

- Existing text-only notes load and save without losing fields or text.
- Create “Xmas gift for wife,” take a photo, accept it, and see it below the text. Restart the app and verify both persist.
- Choose a stored image, restart, and verify it remains available after the original gallery item is removed.
- Add several images, switch notes, and verify correct note ownership and insertion order.
- Cancel camera/picker, deny any required access, or attempt an unavailable source; the note remains usable and no empty attachment appears.
- Background or recreate the app during capture; recover safely without wrong-note attachment or duplicate images.
- Exercise low-storage/save failure, corrupt images, missing files, and failed cleanup. No false save confirmation or loss of existing text/images.
- Remove an image and delete a note; only the corresponding app-owned files are removed after a successful save.
- Verify portrait/landscape source photos, transparency, very large images, unsupported formats, and video rejection.
- Verify thumbnail scrolling, enlarged view, removal controls, keyboard open/closed, and Android Back on a small portrait screen.
- Change thumbnail size, spacing, button labels/colors, and viewer layout in the Unity Editor. Enter/exit Play Mode and confirm the saved settings are respected.
- Test repeated viewer opening and note switching for accumulating texture memory.

Use focused automated tests for old-data compatibility, failed-save rollback, note-ID callback routing, and file cleanup boundaries. Use real-device checks for camera/picker lifecycle and UI behavior; Editor simulation cannot prove those integrations work.

## 10. Definition of done

The Android app can capture or choose still images, save them privately with the intended note, reopen them after restart, and remove them safely. The complete photo interface is inspectable and editable in saved Unity assets. The handover lists assigned references, Inspector settings, Android integration details, tested devices, supported source formats, and any remaining limitations.
