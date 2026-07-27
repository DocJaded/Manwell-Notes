using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ManWellNotesEditor
{
    // Creates persistent TextMesh Pro assets from the packaged font files. The
    // resulting Sekaiwo asset contains the ordered Noto fallback chain.
    public static class NotesFontBuilder
    {
        private const string OutputFolder = "Assets/ManWellNotes/Fonts/Generated";
        private const string PrimaryAssetPath = OutputFolder + "/Sekaiwo Notes.asset";

        private static readonly (string assetName, string sourcePath)[] FallbackSources =
        {
            // General Latin, Greek and Cyrillic coverage comes first.
            ("Noto Sans", "Assets/Resources/Noto_Sans,Noto_Sans_Arabic,Noto_Sans_Hebrew,Noto_Sans_KR,Noto_Sans_Myanmar,etc/Noto_Sans/static/NotoSans-Regular.ttf"),

            // Script-specific fonts appear before the broader CJK families.
            ("Noto Sans Devanagari", "Assets/ManWellNotes/Fonts/Source/NotoSansDevanagari-Variable.ttf"),
            ("Noto Sans Japanese", "Assets/ManWellNotes/Fonts/Source/NotoSansJP-Variable.otf"),
            ("Noto Sans Arabic", "Assets/Resources/Noto_Sans,Noto_Sans_Arabic,Noto_Sans_Hebrew,Noto_Sans_KR,Noto_Sans_Myanmar,etc/Noto_Sans_Arabic/static/NotoSansArabic-Regular.ttf"),
            ("Noto Sans Hebrew", "Assets/Resources/Noto_Sans,Noto_Sans_Arabic,Noto_Sans_Hebrew,Noto_Sans_KR,Noto_Sans_Myanmar,etc/Noto_Sans_Hebrew/static/NotoSansHebrew-Regular.ttf"),
            ("Noto Sans Myanmar", "Assets/Resources/Noto_Sans,Noto_Sans_Arabic,Noto_Sans_Hebrew,Noto_Sans_KR,Noto_Sans_Myanmar,etc/Noto_Sans_Myanmar/static/NotoSansMyanmar-Regular.ttf"),

            // These retain region-appropriate forms for Korean and Chinese text.
            ("Noto Sans Korean", "Assets/Resources/Noto_Sans,Noto_Sans_Arabic,Noto_Sans_Hebrew,Noto_Sans_KR,Noto_Sans_Myanmar,etc/Noto_Sans_KR/static/NotoSansKR-Regular.ttf"),
            ("Noto Sans Simplified Chinese", "Assets/Resources/Noto_Sans,Noto_Sans_Arabic,Noto_Sans_Hebrew,Noto_Sans_KR,Noto_Sans_Myanmar,etc/Noto_Sans_SC/static/NotoSansSC-Regular.ttf"),
            ("Noto Sans Traditional Chinese", "Assets/Resources/Noto_Sans,Noto_Sans_Arabic,Noto_Sans_Hebrew,Noto_Sans_KR,Noto_Sans_Myanmar,etc/Noto_Sans_TC/static/NotoSansTC-Regular.ttf")
        };

        public static TMP_FontAsset BuildPrimaryFont()
        {
            EnsureOutputFolder();
            EnsureTmpSettings();

            Font sekaiwo = LoadSourceFont(
                "Assets/Resources/sekaiwo Font/Sekaiwo-Regular.ttf");

            TMP_FontAsset primary = CreateFontAsset(
                "Sekaiwo Notes",
                sekaiwo,
                PrimaryAssetPath);

            List<TMP_FontAsset> fallbacks = new List<TMP_FontAsset>();
            foreach ((string assetName, string sourcePath) in FallbackSources)
            {
                Font source = LoadSourceFont(sourcePath);
                string outputPath = $"{OutputFolder}/{assetName}.asset";
                fallbacks.Add(CreateFontAsset(assetName, source, outputPath));
            }

            // TextMesh Pro walks this list only when Sekaiwo lacks a character.
            primary.fallbackFontAssetTable = fallbacks;
            TMP_Settings.defaultFontAsset = primary;
            TMP_Settings.fallbackFontAssets = fallbacks;

            // Exercise representative scripts now. Dynamic assets will add other
            // characters from the same source fonts as users type them later.
            const string coverageSample =
                "Sekaiwo · 日本語 · हिन्दी · العربية · עברית · မြန်မာ · 한국어 · 中文";
            if (!primary.HasCharacters(
                    coverageSample,
                    out uint[] missingCharacters,
                    true,
                    true))
            {
                throw new System.InvalidOperationException(
                    $"The notes font chain is missing Unicode values: {string.Join(", ", missingCharacters)}");
            }

            EditorUtility.SetDirty(primary);
            EditorUtility.SetDirty(TMP_Settings.instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return primary;
        }

        private static Font LoadSourceFont(string path)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (source == null)
                throw new System.IO.FileNotFoundException($"Required font was not imported: {path}");
            return source;
        }

        private static TMP_FontAsset CreateFontAsset(string name, Font source, string outputPath)
        {
            // Recreate the generated asset so its atlas settings always match this code.
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath) != null)
                AssetDatabase.DeleteAsset(outputPath);

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                source,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            asset.name = name;
            asset.atlasTextures[0].name = name + " Atlas";
            asset.material.name = name + " Material";

            AssetDatabase.CreateAsset(asset, outputPath);
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ManWellNotes/Fonts"))
                AssetDatabase.CreateFolder("Assets/ManWellNotes", "Fonts");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/ManWellNotes/Fonts", "Generated");
        }

        private static void EnsureTmpSettings()
        {
            // A new Unity project does not necessarily include TMP's shaders and
            // settings. Import Unity's bundled essentials non-interactively once.
            const string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath) != null &&
                Shader.Find("TextMeshPro/Mobile/Distance Field") != null)
                return;

            // Remove a partial settings asset left by an interrupted generation.
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath) != null)
                AssetDatabase.DeleteAsset(settingsPath);

            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            if (package == null)
                throw new System.InvalidOperationException("Could not locate Unity's TextMesh Pro package.");

            string essentialsPath = System.IO.Path.Combine(
                package.resolvedPath,
                "Package Resources",
                "TMP Essential Resources.unitypackage");

            AssetDatabase.ImportPackage(essentialsPath, false);
            AssetDatabase.Refresh();

            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath) == null ||
                Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                throw new System.InvalidOperationException("TextMesh Pro Essentials did not import correctly.");
            }
        }
    }
}
