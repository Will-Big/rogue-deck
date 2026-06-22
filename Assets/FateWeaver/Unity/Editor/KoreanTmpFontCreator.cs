using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>Creates a dynamic Korean TMP font asset from the OS Malgun Gothic. The TTF is copied into
    /// the project (a dynamic atlas reads glyphs from the source font on demand). The generated
    /// Resources/Fonts artifacts are gitignored — run this once per machine.</summary>
    public static class KoreanTmpFontCreator
    {
        private const string FontFolder = "Assets/FateWeaver/Unity/Resources/Fonts";
        private const string TtfAssetPath = FontFolder + "/malgun.ttf";
        private const string FontAssetPath = FontFolder + "/KoreanTMP.asset";
        private const string SourceTtf = "C:/Windows/Fonts/malgun.ttf";

        [MenuItem("Fate Weaver/Create Korean TMP Font")]
        public static void Create()
        {
            Directory.CreateDirectory(FontFolder);

            if (!File.Exists(SourceTtf))
            {
                Debug.LogError("Source font not found: " + SourceTtf
                    + ". Use the manual Font Asset Creator fallback (see PLAYTEST.md).");
                return;
            }

            if (!File.Exists(TtfAssetPath))
            {
                File.Copy(SourceTtf, TtfAssetPath, overwrite: true);
                AssetDatabase.ImportAsset(TtfAssetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(TtfAssetPath);
            if (sourceFont == null)
            {
                Debug.LogError("Failed to import " + TtfAssetPath
                    + ". Use the manual Font Asset Creator fallback (see PLAYTEST.md).");
                return;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                samplingPointSize: 36,
                atlasPadding: 5,
                renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
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

            if (fontAsset.material != null)
            {
                fontAsset.material.name = "KoreanTMP Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (fontAsset.atlasTexture != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            Debug.Log("Created Korean TMP font asset at " + FontAssetPath);
        }
    }
}
