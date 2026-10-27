using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ManWellNotes
{
    // This is the information saved for one note. It contains no UI code, which
    // keeps the saved-data format independent from how the note is displayed.
    [Serializable]
    public sealed class NoteData
    {
        public string id;
        public string title;
        public string body;
        public long createdUtcTicks;
        public long modifiedUtcTicks;
        public bool pinned;
        public NotePhotoData photo;
        public NoteAudioData audio;

        public static NoteData Create()
        {
            // GUIDs remain unique even if notes are later imported from another device.
            long now = DateTime.UtcNow.Ticks;
            return new NoteData
            {
                id = Guid.NewGuid().ToString("N"),
                title = string.Empty,
                body = string.Empty,
                createdUtcTicks = now,
                modifiedUtcTicks = now
            };
        }
    }

    [Serializable]
    public sealed class NoteAudioData
    {
        public string id;
        public string path;
        public long durationMs;
        public static bool Exists(NoteAudioData audio) => audio != null && !string.IsNullOrEmpty(audio.path);
    }

    [Serializable]
    public sealed class NotePhotoData
    {
        public string id;
        public string imagePath;
        public string thumbnailPath;
        public int width;
        public int height;

        public static bool Exists(NotePhotoData photo) => photo != null && !string.IsNullOrEmpty(photo.imagePath);
        public static string Identity(NotePhotoData photo) => Exists(photo) ? photo.id ?? string.Empty : string.Empty;
    }

    [Serializable]
    internal sealed class NoteCollection
    {
        // JsonUtility cannot serialize a top-level List, so it is wrapped in this class.
        public List<NoteData> notes;
    }

    // NoteStore is the small persistence layer. The rest of the app asks it to
    // load or save and does not need to know where the JSON file lives.
    public sealed class NoteStore
    {
        private readonly string filePath;
        private bool recoveredBackup;
        public bool LoadSucceeded { get; private set; } = true;
        public string LastError { get; private set; }

        public NoteStore(string directory = null)
        {
            filePath = Path.Combine(directory ?? Application.persistentDataPath, "man-well-notes.json");
        }

        public List<NoteData> Load()
        {
            if (!File.Exists(filePath) && !File.Exists(filePath + ".bak"))
                return new List<NoteData>();

            try
            {
                return Read(filePath);
            }
            catch (Exception exception)
            {
                try
                {
                    var recovered = Read(filePath + ".bak");
                    recoveredBackup = true;
                    Debug.LogWarning("Recovered the previous saved notes backup.");
                    return recovered;
                }
                catch
                {
                    LoadSucceeded = false;
                    LastError = "Notes could not be opened. The existing files have been preserved.";
                    Debug.LogWarning($"Could not load notes: {exception.Message}");
                    return new List<NoteData>();
                }
            }
        }

        public bool Save(List<NoteData> notes)
        {
            if (!LoadSucceeded) return false;
            try
            {
                // persistentDataPath points to a writable, per-application folder on
                // Windows, Android and other Unity-supported platforms.
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(new NoteCollection { notes = notes }, true);
                AtomicFile.Write(filePath, json, recoveredBackup ? null : filePath + ".bak");
                recoveredBackup = false;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = "Could not save notes. Check that the device has free storage and try again.";
                Debug.LogWarning($"Could not save notes: {exception.Message}");
                return false;
            }
        }

        private static List<NoteData> Read(string path)
        {
            string json = File.ReadAllText(path);
            // Our on-disk wrapper has one top-level notes array. Unity 6 otherwise turns
            // an absent/null List into an empty notebook, so require that wrapper first.
            if (!System.Text.RegularExpressions.Regex.IsMatch(json, @"\A\s*\{\s*""notes""\s*:\s*\["))
                throw new InvalidDataException("Missing notes array.");
            var collection = JsonUtility.FromJson<NoteCollection>(json);
            if (collection?.notes == null) throw new InvalidDataException("Missing notes collection.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var note in collection.notes)
                if (note == null || string.IsNullOrEmpty(note.id) || !ids.Add(note.id))
                    throw new InvalidDataException("Invalid or duplicate note identity.");
            return collection.notes;
        }

        // A backup may still reference the previous picture. Do not remove its files.
        public List<NoteData> ReadBackupForCleanup()
        {
            if (!LoadSucceeded) return null;
            if (!File.Exists(filePath + ".bak")) return new List<NoteData>();
            try { return Read(filePath + ".bak"); }
            catch { return null; }
        }
    }

    internal static class AtomicFile
    {
        public static void Write(string path, string value, string backup = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".writing";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
                {
                    byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(value);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, backup);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
