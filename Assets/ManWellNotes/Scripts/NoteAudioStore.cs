using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ManWellNotes
{
    public sealed class NoteAudioStore
    {
        private readonly string root;
        public NoteAudioStore(string directory) { root = Path.GetFullPath(directory); }
        public static string Timestamp(DateTime localTime) => localTime.ToString("yyyy_MM_dd_HH_mm", CultureInfo.InvariantCulture);
        public static string RelativePath(string noteId, string timestamp) => $"audio/{noteId}/{timestamp}.m4a";
        public string Resolve(string relative)
        {
            string[] parts = (relative ?? "").Split('/');
            if (parts.Length != 3 || parts[0] != "audio" || !Guid.TryParseExact(parts[1], "N", out _) ||
                !parts[2].EndsWith(".m4a", StringComparison.Ordinal) ||
                !DateTime.TryParseExact(parts[2].Substring(0, parts[2].Length - 4), "yyyy_MM_dd_HH_mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new InvalidDataException("Invalid recording path.");
            return Path.Combine(root, parts[0], parts[1], parts[2]);
        }
        public void Cleanup(IEnumerable<NoteData> current, IEnumerable<NoteData> backup, string pendingPath)
        {
            if (backup == null) return;
            var keep = new HashSet<string>(StringComparer.Ordinal) { pendingPath ?? "" };
            foreach (var notes in new[] { current, backup })
                foreach (var note in notes)
                    if (NoteAudioData.Exists(note.audio)) keep.Add(note.audio.path);
            string directory = Path.Combine(root, "audio");
            if (!Directory.Exists(directory)) return;
            foreach (string folder in Directory.GetDirectories(directory))
                foreach (string file in Directory.GetFiles(folder, "*.m4a"))
                {
                    string relative = $"audio/{Path.GetFileName(folder)}/{Path.GetFileName(file)}";
                    try { Resolve(relative); } catch { continue; }
                    if (!keep.Contains(relative)) File.Delete(file);
                }
        }
    }
}
