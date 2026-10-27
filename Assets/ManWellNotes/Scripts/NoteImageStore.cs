using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ManWellNotes
{
    [Serializable]
    public sealed class PendingNotePhoto
    {
        public string id;
        public string noteId;
        public string expectedPhotoId;
    }

    // Owns only generated PNGs beneath images/. Never deletes a gallery source.
    public sealed class NoteImageStore
    {
        private readonly string root;
        private string PendingPath => Path.Combine(root, "pending-photo.json");

        public NoteImageStore(string directory) { root = Path.GetFullPath(directory); }

        public PendingNotePhoto ReadPending()
        {
            if (!File.Exists(PendingPath)) return null;
            var request = JsonUtility.FromJson<PendingNotePhoto>(File.ReadAllText(PendingPath));
            if (request == null || !Guid.TryParseExact(request.id, "N", out _) ||
                !Guid.TryParseExact(request.noteId, "N", out _))
                throw new InvalidDataException("The pending picture request could not be read.");
            return request;
        }

        public void SavePending(PendingNotePhoto request) => AtomicFile.Write(PendingPath, JsonUtility.ToJson(request));
        public void ClearPending() { if (File.Exists(PendingPath)) File.Delete(PendingPath); }

        public string Resolve(string relative)
        {
            if (string.IsNullOrEmpty(relative)) throw new InvalidDataException("Missing image path.");
            string[] parts = relative.Replace('\\', '/').Split('/');
            if (parts.Length != 3 || parts[0] != "images" || !Guid.TryParseExact(parts[1], "N", out _))
                throw new InvalidDataException("Invalid image path.");
            string name = parts[2];
            if (!name.EndsWith(".png", StringComparison.Ordinal)) throw new InvalidDataException("Invalid image extension.");
            string id = name.Substring(0, name.Length - 4);
            if (id.EndsWith("-thumb", StringComparison.Ordinal)) id = id.Substring(0, id.Length - 6);
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid image filename.");
            return Path.Combine(root, "images", parts[1], name);
        }

        public NotePhotoData Import(string normalizedPng, PendingNotePhoto request, int thumbnailSize = 320)
        {
            var photo = new NotePhotoData
            {
                id = request.id,
                imagePath = $"images/{request.noteId}/{request.id}.png",
                thumbnailPath = $"images/{request.noteId}/{request.id}-thumb.png"
            };
            string imagePath = Resolve(photo.imagePath);
            string thumbPath = Resolve(photo.thumbnailPath);
            Texture2D image = null;
            Texture2D thumbnail = null;
            try
            {
                image = LoadBoundedPng(normalizedPng);
                photo.width = image.width;
                photo.height = image.height;
                int longest = Mathf.Clamp(thumbnailSize, 64, 512);
                float ratio = Mathf.Min(1f, (float)longest / Mathf.Max(image.width, image.height));
                int width = Mathf.Max(1, Mathf.RoundToInt(image.width * ratio));
                int height = Mathf.Max(1, Mathf.RoundToInt(image.height * ratio));
                thumbnail = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        pixels[y * width + x] = image.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
                thumbnail.SetPixels(pixels);
                thumbnail.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
                WriteNew(imagePath, image.EncodeToPNG());
                WriteNew(thumbPath, thumbnail.EncodeToPNG());
                return photo;
            }
            catch
            {
                // The caller invokes Import only if this request has not already committed.
                if (File.Exists(imagePath)) File.Delete(imagePath);
                if (File.Exists(thumbPath)) File.Delete(thumbPath);
                throw;
            }
            finally { Release(image); Release(thumbnail); }
        }

        private static void WriteNew(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public Texture2D Load(string relative) => LoadBoundedPng(Resolve(relative));

        public static Texture2D LoadBoundedPng(string path)
        {
            var file = new FileInfo(path);
            if (file.Length < 33 || file.Length > 24 * 1024 * 1024)
                throw new InvalidDataException("The image file is missing or invalid.");
            byte[] bytes = File.ReadAllBytes(path);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < signature.Length; i++)
                if (bytes[i] != signature[i]) throw new InvalidDataException("A normalized PNG image is required.");
            if (bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
                throw new InvalidDataException("Invalid PNG header.");
            long width = ReadBigEndian(bytes, 16), height = ReadBigEndian(bytes, 20);
            if (width < 1 || height < 1 || width > 2048 || height > 2048)
                throw new InvalidDataException("Use an image no larger than 2048 pixels along each edge.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes)) throw new InvalidDataException("The image could not be decoded.");
                return texture;
            }
            catch { Release(texture); throw; }
        }

        private static long ReadBigEndian(byte[] bytes, int offset) =>
            ((long)bytes[offset] << 24) | ((long)bytes[offset + 1] << 16) | ((long)bytes[offset + 2] << 8) | bytes[offset + 3];

        public void Cleanup(IEnumerable<NoteData> current, IEnumerable<NoteData> backup, PendingNotePhoto pending)
        {
            if (backup == null) return; // Unknown backup state: preserve files.
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in new[] { current, backup })
                foreach (var note in group)
                    if (note != null && NotePhotoData.Exists(note.photo))
                    {
                        keep.Add(note.photo.imagePath);
                        keep.Add(note.photo.thumbnailPath);
                    }
            string images = Path.Combine(root, "images");
            if (!Directory.Exists(images)) return;
            foreach (string folder in Directory.GetDirectories(images))
            {
                string noteId = Path.GetFileName(folder);
                if (!Guid.TryParseExact(noteId, "N", out _)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.png"))
                {
                    string name = Path.GetFileName(file);
                    string relative = $"images/{noteId}/{name}";
                    try { Resolve(relative); } catch { continue; }
                    if (keep.Contains(relative) || (pending != null && noteId == pending.noteId &&
                        (name == pending.id + ".png" || name == pending.id + "-thumb.png"))) continue;
                    try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }

        public static void Release(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
