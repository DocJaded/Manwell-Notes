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
    public static class NoteAudioValidation
    {
        private static int checks;
        [MenuItem("Tools/Man-Well Notes/Sound/Run Recording Storage Checks")]
        public static void Run()
        {
            checks = 0;
            string root = Path.GetFullPath(".utmp/AudioChecks/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var store = new NoteStore(root);
            var files = new NoteAudioStore(root);
            var fixture = new GameObject("Audio validation fixture");
            var app = fixture.AddComponent<NotesApp>();
            var controller = fixture.AddComponent<NoteAudioController>();
            try
            {
                Set(app, "store", store);
                var notes = (List<NoteData>)Get(app, "notes");
                var original = NoteData.Create(); original.title = "Existing note"; original.body = "Preserve text";
                notes.Add(original);
                Check(store.Save(notes), "Initial save");
                string stamp = NoteAudioStore.Timestamp(new DateTime(2026, 10, 27, 9, 5, 59));
                Check(stamp == "2026_10_27_09_05", "Timestamp uses requested local-time format");
                var first = app.CreateRecordingNote(stamp);
                var second = app.CreateRecordingNote(stamp);
                Check(first != null && second != null && first.id != second.id && first.title == stamp && first.body == "", "Each recording creates a separate editable timestamp note with blank text");
                Check(original.body == "Preserve text" && original.title == "Existing note", "Creating recording notes preserves existing notes");
                string path = NoteAudioStore.RelativePath(first.id, stamp);
                string otherPath = NoteAudioStore.RelativePath(second.id, stamp);
                Check(Path.GetFileName(files.Resolve(path)) == stamp + ".m4a" && files.Resolve(path) != files.Resolve(otherPath), "Same-minute filenames do not collide");
                Reject(() => files.Resolve("../outside.m4a"));
                Reject(() => files.Resolve(Path.GetFullPath("outside.m4a")));
                Directory.CreateDirectory(Path.GetDirectoryName(files.Resolve(path)));
                File.WriteAllText(files.Resolve(path), "fixture: native decoder tested on device");
                var audio = new NoteAudioData { id = Guid.NewGuid().ToString("N"), path = path, durationMs = 12345 };
                Set(app, "selectedNote", second);
                first.audio = new NoteAudioData(); // JsonUtility can materialize absent nested data during reload.
                Set(controller, "app", app); Set(controller, "files", files);
                var result = new AudioState { id = audio.id, noteId = first.id, path = path, phase = "ready", durationMs = 12345 };
                Set(controller, "state", result);
                Invoke(controller, "AttachCompleted");
                Check(first.audio?.id == audio.id && second.audio == null, "Delayed result attaches only to the original recording note");
                var loaded = new NoteStore(root).Load();
                Check(loaded[1].audio.durationMs == 12345 && loaded[1].audio.path == path && loaded[1].body == "", "Recording metadata survives restart");
                result.phase = "ready"; Invoke(controller, "AttachCompleted");
                Check(notes.Count == 3 && first.audio.id == audio.id, "Completed request replay does not duplicate a note or attachment");
                Check(!app.TrySetAudio(first.id, "wrong-id", null), "Stale removal cannot change a different recording");
                string jsonPath = Path.Combine(root, "man-well-notes.json");
                string before = File.ReadAllText(jsonPath);
                Directory.CreateDirectory(jsonPath + ".writing");
                Check(!app.TrySetAudio(first.id, audio.id, null) && first.audio.id == audio.id && File.ReadAllText(jsonPath) == before, "Failed removal preserves audio and saved JSON");
                Check(app.CreateRecordingNote(stamp) == null && notes.Count == 3, "Failed save creates no recording note");
                Directory.Delete(jsonPath + ".writing");
                Check(app.TrySetAudio(first.id, audio.id, null), "Removal succeeds");
                files.Cleanup(notes, store.ReadBackupForCleanup(), null);
                Check(File.Exists(files.Resolve(path)), "Backup protects previous audio");
                app.TrySaveNotes(); files.Cleanup(notes, store.ReadBackupForCleanup(), path);
                Check(File.Exists(files.Resolve(path)), "An unfinished recording is protected from cleanup");
                files.Cleanup(notes, store.ReadBackupForCleanup(), null);
                Check(!File.Exists(files.Resolve(path)), "Unreferenced audio is cleaned after backup advances");
                Check(!app.TrySetAudio(Guid.NewGuid().ToString("N"), "", audio), "A deleted note cannot receive a recording");
                Set(controller, "state", new AudioState { noteId = second.id, phase = "recording" });
                Check(controller.Protects(second.id) && !controller.Protects(first.id), "Active recording note is protected from deletion");
                Debug.Log("NOTE_AUDIO_STORAGE_CHECKS_PASSED: " + checks);
            }
            finally { Object.DestroyImmediate(fixture); }
        }
        private static object Get(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
        private static void Invoke(object o, string name) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);
        private static void Check(bool ok, string message) { if (!ok) throw new BuildFailedException(message); checks++; }
        private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { checks++; return; } throw new BuildFailedException("Unsafe recording path was accepted"); }
    }
}
