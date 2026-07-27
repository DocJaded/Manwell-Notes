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
    internal sealed class NoteCollection
    {
        // JsonUtility cannot serialize a top-level List, so it is wrapped in this class.
        public List<NoteData> notes = new List<NoteData>();
    }

    // NoteStore is the small persistence layer. The rest of the app asks it to
    // load or save and does not need to know where the JSON file lives.
    public sealed class NoteStore
    {
        private readonly string filePath =
            Path.Combine(Application.persistentDataPath, "man-well-notes.json");

        public List<NoteData> Load()
        {
            if (!File.Exists(filePath))
                return new List<NoteData>();

            try
            {
                string json = File.ReadAllText(filePath);
                NoteCollection collection = JsonUtility.FromJson<NoteCollection>(json);
                return collection?.notes ?? new List<NoteData>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not load notes: {exception.Message}");
                return new List<NoteData>();
            }
        }

        public void Save(List<NoteData> notes)
        {
            try
            {
                // persistentDataPath points to a writable, per-application folder on
                // Windows, Android and other Unity-supported platforms.
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(new NoteCollection { notes = notes }, true);
                File.WriteAllText(filePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save notes: {exception.Message}");
            }
        }
    }
}
