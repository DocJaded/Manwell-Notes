# Man-Well Notes — Project Handover

Last updated: 30 July 2026  
Unity version: 6000.3.7f1  
Repository: `https://github.com/DocJaded/Manwell-Notes.git`  
Primary branch: `main`

## 1. Project summary

Man-Well Notes is a small, offline Android notes application built with Unity UI
and TextMesh Pro. The current interface is portrait-only and uses three full-screen
pages:

1. Library — entry points for all notes, pinned notes, and creating a note.
2. Notes list — search, note cards, and navigation back to the library.
3. Editor — title, creation timestamp, body, Pin/Unpin, Delete, and Back.

Notes are stored locally as JSON. There is currently no account, cloud sync,
encryption, export, import, backup, or conflict handling.

The only enabled build scene is:

`Assets/Scenes/SampleScene.unity`

## 2. Current user-visible behavior

- The app opens on the Library page.
- All notes opens the complete list.
- Pinned opens the list with the internal `is:pinned` filter.
- New note creates and immediately opens a blank note.
- Selecting a card opens that note in the editor.
- Pin and Unpin refresh the list and automatically return to the notes page.
- Delete opens a confirmation dialog and returns to the notes list after confirmation.
- Long-pressing a note card for about 0.65 seconds opens the same delete confirmation.
- Notes are sorted with pinned notes first, then by most recently modified.
- Search matches title and body using case-insensitive substring matching.
- Each card shows:
  - `PINNED ·` when appropriate;
  - title, with `Untitled note` as the fallback;
  - a one-line body preview;
  - local creation date and time.
- The editor shows the same creation date and time below the title.
- Long titles autosize only within a readable range: 34–18 in the editor and
  20–16 on note cards.
- The title rejects further input when it can no longer fit at 18 points (with an
  80-character absolute safeguard). The body rejects input beyond 10,000
  characters. Rejected typing or pasted text is not saved, and a red warning
  appears until the user removes text.
- Title and body use Android's native editing surface: touch caret placement,
  selection handles, and the normal Cut/Copy/Paste/Select all floating menu.
- The body has a persistent vertical scrollbar. Dragging its handle allows long
  notes to be read from beginning to end whether or not the keyboard is open.
- Android Back moves Editor → Notes → Library → quit.
- The app adjusts its bottom inset while the Android keyboard is visible.

## 3. Architecture and important files

### Runtime

`Assets/ManWellNotes/Scripts/NotesApp.cs`

- Owns the application state and most UI behavior.
- Binds button/input events.
- Creates runtime note cards.
- Handles filtering, sorting, pinning, deleting, and autosave.
- Contains the UI construction code used by the scene builder.
- Uses fixed accessible text sizes and a `360 × 640` Canvas reference.
- Builds the delete confirmation and configures native mobile text editing for
  fresh scene builds and the in-place migration.

`Assets/ManWellNotes/Scripts/LongPressButton.cs`

- Detects a deliberate 0.65-second hold on a note card.
- Suppresses the normal open-note click after a successful long press.

`Assets/ManWellNotes/Scripts/NoteData.cs`

- Defines the serialized note model:
  - `id`
  - `title`
  - `body`
  - `createdUtcTicks`
  - `modifiedUtcTicks`
  - `pinned`
- Contains `NoteStore`, which reads and writes the JSON file.

`Assets/ManWellNotes/Scripts/NotesPageNavigator.cs`

- Activates exactly one of the three saved page GameObjects.
- Handles Android Back through the Input System's Escape mapping.

`Assets/ManWellNotes/Scripts/KeyboardSafeArea.cs`

- Applies safe-area and soft-keyboard bottom insets.
- Runs continuously while enabled because Android keyboard geometry may arrive late.

### Editor utilities

All tools are under Unity's `Tools > Man-Well Notes` menu.

`Assets/ManWellNotes/Editor/NotesSceneBuilder.cs`

- Rebuilds the app hierarchy in `SampleScene`.
- Rebuilds the TextMesh Pro font chain first.
- Deletes and recreates only the GameObject named `Man-Well Notes`.

`Assets/ManWellNotes/Editor/PortraitPageMigration.cs`

- Converts the rebuilt hierarchy into the three-page portrait layout.
- Creates or repairs Back buttons and the creation timestamp.
- Assigns serialized page/navigation references.
- Applies the accessible typography.
- Removes the obsolete custom clipboard toolbar, enables native mobile editing,
  and adds or repairs delete-confirmation controls without rebuilding the app.
- Sets the Canvas reference to `360 × 640`.

`Assets/ManWellNotes/Editor/KeyboardLayoutMigration.cs`

- Moves the toolbar to the bottom.
- Adds the keyboard-safe component without rebuilding the interface.

`Assets/ManWellNotes/Editor/NotesFontBuilder.cs`

- Generates the Sekaiwo TextMesh Pro font asset.
- Builds the ordered Noto fallback chain for multiple writing systems.
- Imports TMP essentials if they are missing.

`Assets/ManWellNotes/Editor/AndroidProjectSetup.cs`

- Restores the documented Android settings and switches the active target.

## 4. Data storage

The notes file is:

`Path.Combine(Application.persistentDataPath, "man-well-notes.json")`

On Android, uninstalling the app normally removes this app-local data. Installing a
new build over the existing package should retain it as long as the application ID
and signing identity are unchanged.

Saving behavior:

- Text changes use a 0.45-second debounce.
- Pin/Unpin queues a save.
- Delete saves immediately.
- Application pause and quit save immediately.
- Legacy notes without a creation timestamp are repaired using their modification
  timestamp, or the current time if no usable timestamp exists.

Important limitations:

- Writes replace the JSON file directly and are not atomic.
- There is no backup file or recovery flow.
- A parse/read exception is logged and an empty list is returned.
- A crash within the debounce window can lose the last fraction of a second of edits.
- “Stored privately on this device” means local-only; the JSON is not encrypted.

## 5. Scene and UI rules

The scene contains the persistent page hierarchy, but note cards are runtime objects.
Editing a card in the Unity hierarchy will not change the cards seen in the app.
Change `BuildNoteCard` in `NotesApp.cs` instead.

Runtime cards use explicit anchored rectangles for title, preview, and timestamp.
This is intentional. A nested layout group previously produced blank-looking cards
on the device even though the card backgrounds existed.

Text generally uses fixed accessible sizes rather than global TMP autosizing.
Autosizing is enabled only for note titles and has strict readable minimums
(18 in the editor and 16 on cards). If another translated label later requires
autosizing, give it its own tested minimum.

The body scrollbar is assigned directly to `TMP_InputField.verticalScrollbar`.
The body also uses `RectMask2D` so scrolled text stays clipped to the writing area.
Keep the scrollbar separate from the main text surface: this preserves Android's
touch caret and native selection gestures while providing an unambiguous scroll
target on the right edge.

The current design assumes:

- portrait orientation;
- one page visible at a time;
- a `360 × 640` logical reference;
- exact page and control names used by the migration utilities.

## 6. Rebuilding or repairing the scene

### Preferred repair workflow

For an existing scene where only layout or references are wrong:

1. Run `Tools > Man-Well Notes > Apply Portrait Page Layout`.
2. If only keyboard behavior is wrong, run
   `Tools > Man-Well Notes > Apply Keyboard-Safe Layout`.
3. Save and test `SampleScene`.

### Full rebuild workflow

Use this only when the app hierarchy is badly damaged:

1. Commit or back up the scene first.
2. Run `Tools > Man-Well Notes > Rebuild Notes Interface`.
3. Run `Tools > Man-Well Notes > Apply Portrait Page Layout`.
4. Check any manually assigned sprites or scene-only customizations.
5. Test all three pages before committing.

### Critical rebuild pitfall

`Rebuild Notes Interface` destroys the entire GameObject named `Man-Well Notes`.
Any manual changes beneath that object that are not represented in
`NotesApp.BuildSceneInterface` will be lost. The current two referenced icons may
need to be reassigned after a full rebuild because the builder creates basic
background Images and does not encode every later scene customization.

The migration scripts also depend on exact GameObject names such as `App`,
`Sidebar`, `Notes list`, `Editor`, and `Toolbar`. Renaming them can make a migration
throw an exception or update the wrong object.

## 7. Fonts and international text

Primary face:

`Assets/ManWellNotes/Fonts/Generated/Sekaiwo Notes.asset`

The generated asset has dynamic Noto fallbacks for Latin, Greek, Cyrillic,
Devanagari, Japanese, Arabic, Hebrew, Myanmar, Korean, Simplified Chinese, and
Traditional Chinese.

Pitfalls:

- `NotesFontBuilder` has hard-coded source-font paths.
- Moving a required source font requires updating the builder before rebuilding.
- The source files are dynamic-font dependencies; do not remove the required fonts
  without validating runtime glyph generation on Android.
- Preserve source files as UTF-8. The code contains `‹`, `…`, `·`, and multilingual
  coverage text. An incorrect encoding can turn these into mojibake.
- Font fallback and right-to-left glyph coverage exist, but full bidirectional
  layout and language-specific UI testing have not been completed.

## 8. Icons and large source assets

Only two Spooky Icons assets are referenced by the scene. They are kept under:

`Assets/ManWellNotes/Icons`

Their original `.meta` files and GUIDs were preserved.

`Assets/Resources/Layer 17.png` is a newly added knife illustration. It is not
currently assigned to a control. Because it is in `Resources`, Unity will include
it in a build even while unreferenced; move it out of `Resources` if it is not
intended for runtime use.

The unused Spooky Icons library is archived outside Unity's `Assets` directory:

`SourceAssets/Spooky Icons Library`

Do not move that archive back under `Assets` or `Assets/Resources`. It contains
about 1.96 GB across more than 8,000 files and previously caused extremely large,
slow APK builds.

The archive remains version-controlled and its PNG/PSD data is handled by Git LFS.

## 9. Android build configuration

Current settings:

- Application ID: `com.manwell.notes`
- Product: `Man-Well Notes`
- Company: `Man-Well`
- Version name: `0.1.0`
- Version code: `1`
- Minimum Android API: 26
- Target API: Auto
- Architecture: ARM64
- Scripting backend: IL2CPP
- Android entry point: Activity (not GameActivity)
- Orientation: Portrait only
- Prototype output: APK, not AAB

Run `Tools > Man-Well Notes > Configure Project for Android` if these settings are
lost.

The Activity entry point is intentional. Unity's GameActivity always hides the
native keyboard input field, which prevents the familiar Android selection handles
and floating Cut/Copy/Paste/Select all menu used by this app.

The previous APK in `Release_APK` was built before icon cleanup and is stale. APK,
AAB, `Release_APK`, `Library`, `Temp`, and Logs are intentionally ignored by Git.

## 10. Build-size and build-time pitfalls

The original `Assets/Resources` payload was approximately 2.27 GB and produced an
approximately 1.85 GB APK. Archiving the unused icon pack reduced Resources to
about 307 MB.

The next major build-size issue is the raw Noto font collection under
`Assets/Resources`. Because Unity includes everything in a Resources folder, the
entire collection is packaged even though the app uses only a subset. A future
cleanup should:

1. Identify every font source required by `NotesFontBuilder`.
2. Keep required runtime sources and generated TMP assets.
3. Move unused weights, widths, variants, documentation, and duplicate families
   outside `Assets`.
4. Update hard-coded font paths.
5. Rebuild the font assets.
6. Test all supported scripts on Android before deleting or archiving anything.

Other build-time contributors:

- IL2CPP native compilation, especially on clean builds.
- URP and several unused template packages.
- First-time shader/package import.
- Deleting `Library` defeats Unity's incremental import and build caches.

Do not treat `Library` cleanup as routine build optimization; it usually makes the
next build substantially slower.

## 11. Git and repository rules

- Binary images and font files use Git LFS through `.gitattributes`.
- Unity text assets and scripts use LF line endings.
- Always move a Unity asset together with its `.meta` file.
- Preserving the `.meta` file preserves the asset GUID and scene references.
- Do not commit APKs, AABs, `Library`, `Temp`, Logs, or user settings.
- The large `SourceAssets` archive is outside Unity but remains part of the Git
  repository.

## 12. Known risks and unfinished areas

- The editor-side `InteractionValidation` utility checks card-label bounds and the
  new interaction references, but persistence and navigation still lack automated
  tests.
- Delete has confirmation, but there is no undo, trash, or restore.
- No data export/import or backup.
- No atomic-save or corrupted-file recovery strategy.
- No user-selectable text scale; accessibility currently relies on larger sizes
  plus bounded title autosizing.
- No localization system despite broad font coverage.
- Right-to-left language layout has not been fully validated.
- Keyboard safe-area behavior should be tested on multiple Android vendors and
  gesture/navigation modes.
- Search is rebuilt on every input change and every edit. This is acceptable for a
  small note count but will become inefficient with hundreds or thousands of notes.
- `RefreshList` destroys and recreates every visible card. Consider pooling or
  incremental updates if the data set grows.
- Pinning and unpinning both return to the notes list. This is deliberate but should
  be reconsidered if users expect to continue editing.
- All notes live in memory and the entire collection is rewritten on save.
- Current app-category settings still identify the Android build as a game in parts
  of Unity's generated settings; review store metadata before release.
- Signing/release keystore configuration has not been documented or committed.

## 13. Minimum manual smoke test

Before an APK handoff:

1. Launch and confirm the Library page is first.
2. Open All notes and Pinned, then test Android Back.
3. Create a note and enter a title and multiline body.
4. Confirm its title, preview, and creation timestamp appear on the card.
5. Reopen it and confirm the same timestamp appears below the title.
6. Pin it and confirm the app returns to the list with visible card text.
7. Open Pinned and confirm the note appears.
8. Unpin it and confirm it disappears from the pinned-only list.
9. Search by title and body text.
10. Open the keyboard and confirm the editor/toolbar remain accessible.
11. Background and resume the app, then restart it and confirm persistence.
12. Long-press a card, cancel deletion, and confirm the note still opens normally
    with a short tap.
13. Delete from both the editor button and a card long press; confirm each dialog
    names the note and deletion persists after restart.
14. In both title and body fields, place the caret by touch, drag the native
    selection handles, and test the Android Cut/Copy/Paste/Select all menu.
15. Extend a title until it reaches 18-point text; confirm further typing and
    pasting are rejected and the red limit warning appears.
16. Confirm the body accepts 10,000 characters, rejects additional typing/pasting,
    shows the red warning, and allows input again after deleting text.
17. Close the keyboard and drag the body scrollbar to read a long note all the way
    to its final line; repeat while editing with the keyboard open.
18. Test at least one fallback-script sample if fonts changed.

## 14. Recommended next work

1. Clean the unused Noto font sources to reduce APK size and build time.
2. Produce a fresh APK and record its size/build duration after cleanup.
3. Add atomic saves plus a recoverable backup JSON file.
4. Add an undo/trash workflow for accidental deletion.
5. Add tests for serialization, legacy timestamp repair, filtering, and sorting.
6. Add a user-selectable text-size preference if accessibility remains a priority.
7. Review packages and remove unused 2D/template dependencies carefully.
