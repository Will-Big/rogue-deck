using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>Creates a Korean TMP font asset from the bundled Pretendard TTF
    /// (<see cref="SourceTtfAssetPath"/>, OFL, committed to the repo — works on macOS/Windows/Linux
    /// with no OS font dependency). The generated <c>Resources/Fonts/KoreanTMP.asset</c> is gitignored,
    /// so run this once per machine. The asset is pinned to <see cref="PinnedGuid"/> — the guid the
    /// playtest scenes already reference — so their labels resolve without any manual rewiring.
    ///
    /// Every Hangul glyph used anywhere in the FateWeaver C# sources is <b>pre-baked</b> into the atlas
    /// at generation time. Labels whose text is assigned at runtime (State/Message/Timeline/Piles) would
    /// otherwise show as boxes: a dynamic atlas that is saved as a sub-asset comes back non-readable, so
    /// glyphs the editor never rendered can't be blitted in at runtime. Baking the full in-game character
    /// set up front removes that dependency entirely.</summary>
    public static class KoreanTmpFontCreator
    {
        private const string SourceTtfAssetPath = "Assets/FateWeaver/Unity/Fonts/Pretendard-Regular.ttf";
        private const string FontFolder = "Assets/FateWeaver/Unity/Resources/Fonts";
        private const string FontAssetPath = FontFolder + "/KoreanTMP.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // Roots scanned for Hangul so the atlas covers every string the game can display (card / enemy /
        // scenario names, description vocabulary, and the controller's runtime status text).
        private const string ScanRoot = "Assets/FateWeaver";

        // The playtest scenes (FateWeaverPlaytest / FateWeaverWardenPlaytest) reference this guid for
        // every TMP label. Pinning the generated asset to it makes the scenes render Pretendard directly.
        private const string PinnedGuid = "008df83b1c9db764c8fd208abe909623";

        [MenuItem("Fate Weaver/Create Korean TMP Font")]
        public static void Create()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceTtfAssetPath);
            if (sourceFont == null)
            {
                Debug.LogError("Source font not found: " + SourceTtfAssetPath
                    + ". Ensure the Pretendard TTF is present (see PLAYTEST.md).");
                return;
            }

            Directory.CreateDirectory(FontFolder);

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                samplingPointSize: 36,
                atlasPadding: 5,
                renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlasWidth: 2048,
                atlasHeight: 2048,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("TMP_FontAsset.CreateFontAsset returned null. "
                    + "Use the manual Font Asset Creator fallback (see PLAYTEST.md).");
                return;
            }

            fontAsset.name = "KoreanTMP";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // Keep baked glyphs when building a player (dynamic data is otherwise cleared on build).
            var so = new SerializedObject(fontAsset);
            var clearProp = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearProp != null)
            {
                clearProp.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Register the material + initial atlas texture as sub-assets BEFORE baking, so the pixels
            // TryAddCharacters writes into the atlas are persisted with the asset. (Baking first leaves an
            // empty/unsaved atlas and TMP treats the whole font as unusable, falling back to LiberationSans.)
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "KoreanTMP Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (fontAsset.atlasTexture != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            PrebakeCharacters(fontAsset);

            // Baking can spill into extra atlas textures — persist any that TryAddCharacters created.
            foreach (var atlas in fontAsset.atlasTextures)
            {
                if (atlas != null && !AssetDatabase.Contains(atlas))
                {
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }

            EditorUtility.SetDirty(fontAsset);
            foreach (var atlas in fontAsset.atlasTextures)
            {
                if (atlas != null) EditorUtility.SetDirty(atlas);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);

            PinGuid();
            EnsureTmpFallback();
            Debug.Log("Created Korean TMP font asset at " + FontAssetPath
                + " (guid " + AssetDatabase.AssetPathToGUID(FontAssetPath)
                + ", " + fontAsset.characterTable.Count + " glyphs, "
                + fontAsset.atlasTextures.Length + " atlas texture(s)).");
        }

        // Renders every character the game uses into the atlas now, so no glyph has to be added at
        // runtime. Covers printable ASCII, the punctuation used in status strings, and all Hangul found
        // in the project's C# sources.
        private static void PrebakeCharacters(TMP_FontAsset fontAsset)
        {
            var unicodes = CollectCharacters();
            fontAsset.TryAddCharacters(unicodes.ToArray(), out uint[] missing);
            int missingCount = missing?.Length ?? 0;
            if (missingCount > 0)
            {
                Debug.LogWarning("KoreanTMP: " + missingCount
                    + " character(s) could not be added (not in Pretendard or atlas full).");
            }
        }

        private static List<uint> CollectCharacters()
        {
            var set = new HashSet<uint>();

            for (uint c = 0x20; c <= 0x7E; c++) set.Add(c);          // printable ASCII
            foreach (var c in "·—–…×○●□■◆★☆↑↓←→∞") set.Add(c);      // symbols seen in status/timeline text

            foreach (var file in Directory.GetFiles(ScanRoot, "*.cs", SearchOption.AllDirectories))
            {
                foreach (var c in File.ReadAllText(file))
                {
                    if (IsHangul(c)) set.Add(c);
                }
            }

            return new List<uint>(set);
        }

        private static bool IsHangul(char c)
        {
            return (c >= 0xAC00 && c <= 0xD7A3)     // Hangul syllables
                || (c >= 0x1100 && c <= 0x11FF)     // Hangul Jamo
                || (c >= 0x3130 && c <= 0x318F);    // Hangul Compatibility Jamo
        }

        // Rewrites the generated .meta so the asset carries the guid the scenes expect. No-op if it
        // already matches (e.g. a re-run). PinnedGuid is otherwise unused in the project, so adopting
        // it on reimport is safe.
        private static void PinGuid()
        {
            if (AssetDatabase.AssetPathToGUID(FontAssetPath) == PinnedGuid)
            {
                return;
            }

            var metaPath = FontAssetPath + ".meta";
            if (!File.Exists(metaPath))
            {
                Debug.LogWarning("KoreanTMP.asset.meta missing; scene labels may not resolve. Expected: " + metaPath);
                return;
            }

            var meta = File.ReadAllText(metaPath);
            meta = Regex.Replace(meta, @"guid: [0-9a-fA-F]{32}", "guid: " + PinnedGuid);
            File.WriteAllText(metaPath, meta);
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }

        private static void EnsureTmpFallback()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (settings == null || fontAsset == null)
            {
                Debug.LogWarning("KoreanTMP fallback could not be registered in TMP Settings.");
                return;
            }

            var so = new SerializedObject(settings);
            var fallbackProp = so.FindProperty("m_fallbackFontAssets");
            if (fallbackProp == null)
            {
                Debug.LogWarning("TMP Settings fallback list not found.");
                return;
            }

            for (int i = 0; i < fallbackProp.arraySize; i++)
            {
                if (fallbackProp.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                {
                    return;
                }
            }

            fallbackProp.InsertArrayElementAtIndex(fallbackProp.arraySize);
            fallbackProp.GetArrayElementAtIndex(fallbackProp.arraySize - 1).objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
