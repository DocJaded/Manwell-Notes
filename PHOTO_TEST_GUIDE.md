# Android photo test

This is the first implementation step from PHOTO_FEATURE_PLAN.md: an isolated camera and image-selection test. It does not attach photos to real notes yet.

Updated 10 September 2026: the agreed feature limit is **one picture per note**. In this test, taking or choosing another picture now opens a confirmation saying **“The existing picture will be replaced”**, with **Yes / No** buttons. Yes continues to the chosen source. No keeps the picture, as does cancelling the source or a failed import. The first picture needs no replacement prompt.

## Open and edit in Unity

Open **Tools > Man-Well Notes > Photo Test > Open Test Scene**.

- Scene: `Assets/ManWellNotes/PhotoSpike/Scenes/Photo Test.unity`
- Prefab: `Assets/ManWellNotes/PhotoSpike/Prefabs/Photo Test UI.prefab`
- All buttons, labels, colors, margins, and preview geometry are saved in that prefab. Edit them normally in the Inspector. Apply scene overrides to the prefab when you want those changes shared.
- Button On Click events are wired in the Inspector. Runtime code does not rebuild the interface or reset its layout.
- The **Replacement confirmation** child of the prefab contains the editable warning, panel, and Yes/No buttons. It is saved inactive. **Add Missing Replacement Dialog** upgrades an older test prefab without rebuilding its existing layout.
- **Maximum Image Size** on the root controller sets the Android decode limit (256–2048 pixels along the longest edge).
- **Editor Sample** accepts a texture for testing the preview in Play Mode. Choose image displays this sample in the Editor; Take photo explains that a phone is required. Editor previews do not prove camera integration or persistence.
- **Create Missing Test Assets** only creates absent assets. It never overwrites an existing test prefab or scene.
- **Validate Saved Test Assets** checks required references and button wiring.

The test uses standard Unity UI Text with Unity's built-in font so it can build independently of the production fonts and other assets.

## Build and install

With Android selected as the active platform, use **Tools > Man-Well Notes > Photo Test > Build Android Test APK**.

Output: `Release_APK/NotesPhotoTest.apk`.

Install this APK on the phone and open **Notes Photo Test**. Its application ID is `com.manwell.notes.phototest`, separate from the notes app's `com.manwell.notes`. It uses separate private storage and can be installed alongside the notes app. It is an ARM64 development build, requiring Android 8/API 26 or newer.

The build command supplies the test scene explicitly; it does not replace the production build scene list. It temporarily enables the test's native Android library and restores the original build settings afterward. The library is disabled for ordinary builds. A build guard rejects an accidentally enabled test library when building the production app ID.

## Phone checklist

Record the phone model, Android version, and results here before integrating with real notes.

| Check | Expected result | Result |
| --- | --- | --- |
| Take photo, accept | Return to test app; correctly oriented image and dimensions appear. | User confirmed camera access and successful capture on 10 September 2026; orientation and dimensions not separately confirmed. |
| Take photo, cancel | Previous test image remains. | Pending phone test |
| Choose image | System image picker or older-device document picker opens; selected JPEG/PNG appears. | User confirmed image selection and inclusion on 10 September 2026; noted PNG/JPG restriction. Picker route not reported. |
| Choose image, cancel | Previous test image remains. | Pending phone test |
| Second picture through camera or chooser | Exact replacement message with Yes / No appears before opening the source. | Pending phone test |
| Replacement: No / Android Back | Confirmation closes; current picture remains and no source opens. | No passed on phone, confirmed by user 26 October 2026; Android Back not separately reported. |
| Replacement: Yes, then cancel source | Current picture remains. | Passed on phone, confirmed by user 26 October 2026. |
| Replacement: Yes, accept new picture, restart | Only the newly accepted picture reappears. | Passed on phone, confirmed by user 26 October 2026. |
| Restart test app | Last accepted image reappears from private storage. | Pending phone test |
| Delete original gallery image | Test app retains its own saved copy. | Pending phone test |
| Portrait, landscape, mirrored EXIF | Preview is correctly oriented and never stretched. | Pending phone test |
| Transparency | PNG transparency is retained. | Pending phone test |
| Unsupported image/video | No video capture/selection workflow; unsupported content is rejected without replacing the previous image. | Pending phone test |
| Repeated capture/selection | Controls recover and old images do not accumulate in normal operation. | Pending phone test |
| Background while camera is open | Return safely; accepted result is associated with the same request. | Pending phone test |
| Android activity recreation | Outstanding request resumes or cancels safely; no duplicate capture. | Pending phone test |
| Force-stop during a request | Reopen; if a request remains pending, Reset interrupted request unlocks the test without removing the last saved image. | Pending phone test |
| Clear test image | Saved test copy is removed; original gallery file is untouched. | Pending phone test |
| Missing camera / inaccessible image / low storage | Readable error; previous image remains usable. | Pending phone test |

The status text identifies whether image selection used the system Photo Picker or document-picker fallback. This helps record which route was actually tested.

## Implementation and limits

The test uses a small in-project Java Android library, with no additional third-party package or network dependency. The library has no Unity Java dependency: Unity starts requests and polls durable results through a narrow bridge.

- Camera: native camera activity, full-size output to a content URI; temporary access is limited to the active capture file. No embedded camera preview or CAMERA permission is requested.
- Gallery: native Photo Picker on API 33+ when available; image-only ACTION_OPEN_DOCUMENT fallback on older devices. This spike does not opt into backported Photo Picker installation. No broad storage or photo-library permission is requested.
- Supported input for this first test: JPEG and non-animated PNG. GIF, WebP, HEIC, APNG, videos, and corrupt files are rejected. HEIC and other phone image formats remain an integration decision after the device test.
- Native decoding checks source size (40 MB) and pixel count (100 megapixels), downsamples before allocating an image, applies EXIF rotation/reflection, and re-encodes to PNG. Saved dimensions can be smaller than the configured maximum because Android decodes in power-of-two sample sizes.
- A complete new image is written before its durable reference replaces the previous one. The latest accepted image lives in the test app's internal `files/photo-spike` directory. Clearing app storage or uninstalling the test app removes it.
- Request IDs and phases are persisted to support activity/process recreation. Forced termination can still abandon a request; the explicit Reset interrupted request control provides a safe test escape. Fully automatic interrupted-request recovery is not yet certified.
- The test keeps one accepted image, matching the planned limit of one picture per note. Note ownership, thumbnails, note deletion, and production JSON transactions are later steps.
- A crash during import may leave an unreferenced temporary image. Full orphan cleanup is a production-integration requirement, not claimed by this spike.

## Verification record

- Java bridge compiled against the installed Android API 36 SDK.
- Seven host-side import checks passed: byte preservation, empty/missing/oversized input rejection, still PNG handling, APNG detection, and corrupt chunk rejection. Test source: `Tools/PhotoSpike/PhotoImportChecks.java`. These checks exercise file boundaries, not Android UI.
- Unity 6000.3.7f1 compiled the test and validated its saved references and Inspector events.
- ARM64 IL2CPP development APK built successfully on 9 September 2026. The built manifest confirms the separate test application ID, minimum API 26, and target API 36.
- Inspected the actual APK manifest: no CAMERA, broad storage, or READ_MEDIA permissions; the Unity development build includes INTERNET. The photo bridge itself performs no network operations.
- Rendered and visually checked the saved portrait layout. Preview: `Release_APK/PhotoTest-preview.png`.
- On 10 September, the replacement-flow checks passed in the Editor: first picture without a prompt, No preserves it, stale Yes does nothing, Yes continues selection, camera also prompts, no new result preserves the picture, and adding after clearing needs no prompt. The saved Yes/No dialog was rendered and visually checked in `Release_APK/PhotoTest-replacement-preview.png`; the updated Android APK built and its signature verified successfully. Native camera/picker cancellation still requires the phone checklist.
- Confirmed production scene, scripts, Packages, and ProjectSettings remained unchanged.
- Real-device behavior: on 10 September 2026, the user confirmed camera access/capture and stored-image selection/inclusion, and noted the PNG/JPG restriction. On 26 October, the user confirmed all three follow-up checks: No keeps the picture; Yes followed by source cancellation keeps it; successful replacement persists after restart. Phone model, Android version, picker route, and the other checklist outcomes have not been reported.

Current build fingerprint (SHA-256): `063B8BEFCD6D9916180C752898A9DD8B68C24F8E8103D611F529EA7B62D24EA0`.

## References

The camera output URI and full-size result follow Android's [common camera intents](https://developer.android.com/guide/components/intents-common). The picker design follows Android's [Photo Picker documentation](https://developer.android.com/training/data-storage/shared/photo-picker). Unity imports the bridge as an [Android Library project](https://docs.unity3d.com/6000.0/Documentation/Manual/android-library-project-import.html).
