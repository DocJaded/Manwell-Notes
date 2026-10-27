using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ManWellNotes;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ManWellNotesEditor
{
    public static class NotePhotoValidation
    {
        private static int checks;

        [MenuItem("Tools/Man-Well Notes/Photos/Run Photo Storage Checks")]
        public static void Run()
        {
            checks = 0;
            Check(NotePhotoZoom.PinchOffset(Vector2.zero, new Vector2(20, 10), new Vector2(20, 10), 2)
                == new Vector2(-20, -10), "Zoom preserves the point under an off-center pinch.");
            Check(NotePhotoZoom.PinchOffset(new Vector2(5, 8), Vector2.zero, new Vector2(10, 12), 1)
                == new Vector2(15, 20), "Moving the pinch midpoint pans the image.");
            Check(NotePhotoZoom.ClampOffset(new Vector2(999, -999), new Vector2(400, 100), new Vector2(200, 200))
                == new Vector2(100, 0), "Pan bounds keep short axes centered and long axes inside the viewport.");
            string root = Path.GetFullPath(".utmp/PhotoIntegrationChecks/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string a = Guid.NewGuid().ToString("N"), b = Guid.NewGuid().ToString("N");
            string jsonPath = Path.Combine(root, "man-well-notes.json");
            File.WriteAllText(jsonPath, "{\"notes\":[{\"id\":\"" + a + "\",\"title\":\"Old note\",\"body\":\"Keep this text\",\"createdUtcTicks\":1234,\"modifiedUtcTicks\":2345,\"pinned\":true},{\"id\":\"" + b + "\",\"title\":\"Other note\",\"body\":\"Unchanged\"}]}");
            var store = new NoteStore(root);
            var notes = store.Load();
            Check(store.LoadSucceeded && notes.Count == 2 && !NotePhotoData.Exists(notes[0].photo), "Old notes migrate without a picture.");
            Check(notes[0].body == "Keep this text" && notes[0].pinned && notes[0].createdUtcTicks == 1234, "Old text and metadata are preserved.");
            var images = new NoteImageStore(root);
            ExpectFailure(() => images.Resolve("../../outside.png"), "Traversal is rejected.");
            ExpectFailure(() => images.Resolve(Path.GetFullPath("outside.png")), "Absolute image paths are rejected.");
            var texture = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            texture.SetPixels(new Color[32]);
            texture.Apply();
            string source = Path.Combine(root, "sample.png");
            File.WriteAllBytes(source, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            var otherRequest = Request(b);
            notes[1].photo = images.Import(source, otherRequest);
            Check(store.Save(notes), "Initial atomic save succeeds.");
            var fixture = new GameObject("Photo storage test fixture");
            var app = fixture.AddComponent<NotesApp>();
            var controller = fixture.AddComponent<NotePhotoController>();
            try
            {
                Set(app, "store", store);
                var appNotes = (List<NoteData>)Get(app, "notes");
                appNotes.AddRange(notes);
                Set(app, "selectedNote", notes[1]); // User is now on a different note.
                Set(controller, "app", app);
                Set(controller, "images", images);
                var first = Request(a);
                images.SavePending(first);
                Check(images.ReadPending().noteId == a && images.ReadPending().id == first.id, "Pending request persists note identity.");
                Set(controller, "pending", first);
                Invoke(controller, "AcceptResult", source);
                Check(notes[0].photo.id == first.id && notes[1].photo.id == otherRequest.id, "A late result attaches only to its original note.");
                var reloaded = new NoteStore(root).Load();
                Check(reloaded[0].photo.id == first.id && reloaded[0].body == "Keep this text", "Picture and text survive reload.");
                Check(images.ReadPending() == null, "A committed request is cleared.");
                var loadedThumb = images.Load(notes[0].photo.thumbnailPath);
                Check(loadedThumb.width == 8 && loadedThumb.height == 4, "Thumbnail preserves aspect ratio and does not upscale.");
                NoteImageStore.Release(loadedThumb);

                images.SavePending(first);
                Set(controller, "pending", first);
                Invoke(controller, "AcceptResult", "a-file-that-does-not-exist");
                Check(notes[0].photo.id == first.id && images.ReadPending() == null, "Recovered successful callbacks are idempotent.");
                var previous = notes[0].photo;
                string beforeFailure = File.ReadAllText(jsonPath);
                Directory.CreateDirectory(jsonPath + ".writing"); // Force a write failure before replacement.
                bool saved = app.TrySetPhoto(a, first.id, new NotePhotoData { id = "new", imagePath = "new" }, out _);
                Check(!saved && ReferenceEquals(notes[0].photo, previous), "Failed picture save restores in-memory metadata.");
                Check(File.ReadAllText(jsonPath) == beforeFailure && File.Exists(images.Resolve(previous.imagePath)), "Failed save preserves disk data and the old picture.");
                Set(app, "pendingDeleteNote", notes[0]);
                var deleteDialog = new GameObject("Test delete dialog");
                deleteDialog.transform.SetParent(fixture.transform);
                Set(app, "deleteDialog", deleteDialog);
                Invoke(app, "ConfirmDelete");
                Check(appNotes.Contains(notes[0]) && File.ReadAllText(jsonPath) == beforeFailure, "Failed note deletion restores the note.");
                Directory.Delete(jsonPath + ".writing");

                Check(!app.TrySetPhoto(a, "wrong-photo-id", null, out _) && notes[0].photo.id == first.id, "Stale replacement approval cannot overwrite a changed picture.");
                Check(!app.TrySetPhoto(Guid.NewGuid().ToString("N"), "", null, out _), "A deleted note cannot receive a result.");
                var replacement = Request(a, first.id);
                var replacementPhoto = images.Import(source, replacement);
                Check(app.TrySetPhoto(a, first.id, replacementPhoto, out _), "Replacement commits successfully.");
                images.Cleanup(appNotes, store.ReadBackupForCleanup(), null);
                Check(File.Exists(images.Resolve(previous.imagePath)), "The recovery backup's picture remains available.");
                Check(File.Exists(images.Resolve(notes[1].photo.imagePath)), "Another note's picture is preserved.");
                Check(app.TrySaveNotes(), "A subsequent save advances the recovery backup.");
                images.Cleanup(appNotes, store.ReadBackupForCleanup(), null);
                Check(!File.Exists(images.Resolve(previous.imagePath)) && File.Exists(images.Resolve(replacementPhoto.imagePath)), "Only unreferenced replaced files are cleaned up.");

                var inFlight = Request(a, replacementPhoto.id);
                var inFlightPhoto = images.Import(source, inFlight);
                images.Cleanup(appNotes, store.ReadBackupForCleanup(), inFlight);
                Check(File.Exists(images.Resolve(inFlightPhoto.imagePath)), "An in-flight import is protected from cleanup.");
                images.Cleanup(appNotes, store.ReadBackupForCleanup(), null);
                Check(!File.Exists(images.Resolve(inFlightPhoto.imagePath)), "Abandoned unreferenced imports can be cleaned up.");

                Check(app.TrySetPhoto(a, replacementPhoto.id, null, out _), "Picture removal commits without deleting the note.");
                Check(!NotePhotoData.Exists(new NoteStore(root).Load()[0].photo), "Removal survives restart.");
                File.WriteAllText(jsonPath, "invalid JSON");
                var recovery = new NoteStore(root);
                Check(recovery.Load().Count == 2 && recovery.LoadSucceeded, "A damaged primary file recovers from backup.");
                string damaged = Path.Combine(root, "damaged");
                Directory.CreateDirectory(damaged);
                string damagedPath = Path.Combine(damaged, "man-well-notes.json");
                File.WriteAllText(damagedPath, "{\"unexpected\":true}");
                var broken = new NoteStore(damaged);
                broken.Load();
                Check(!broken.LoadSucceeded && !broken.Save(new List<NoteData>()) && File.ReadAllText(damagedPath) == "{\"unexpected\":true}", "Unreadable notes are not overwritten with an empty notebook.");
                Debug.Log("NOTE_PHOTO_STORAGE_CHECKS_PASSED: " + checks);
            }
            finally { Object.DestroyImmediate(fixture); }
        }

        private static PendingNotePhoto Request(string noteId, string expected = "") =>
            new PendingNotePhoto { id = Guid.NewGuid().ToString("N"), noteId = noteId, expectedPhotoId = expected };
        private static object Get(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        private static void Invoke(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
            checks++;
        }
        private static void ExpectFailure(Action action, string message)
        {
            try { action(); } catch (InvalidDataException) { checks++; return; }
            throw new BuildFailedException(message);
        }
    }
}
