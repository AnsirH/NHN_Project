using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ClayWars.Build
{
    /// <summary>
    /// Converts the Korean font asset from a baked static atlas to a dynamic one.
    ///
    /// The baked atlas is a 4096x4096 page covering the full Hangul syllable range,
    /// which costs 16.9 MB of the build while a session shows a few hundred of those
    /// glyphs. Dynamic mode ships the source TTF instead and rasterises on demand.
    /// </summary>
    public static class WebGLFontSettings
    {
        private const string FontAsset = "Assets/GameAssets/Font/DNFForgedBlade-Bold SDF.asset";
        private const string SourceFont = "Assets/GameAssets/Font/DNFForgedBlade-Bold.ttf";

        [MenuItem("ClayWars/WebGL/Font - make the Korean atlas dynamic")]
        public static void MakeDynamic()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);
            if (font == null) { Debug.LogError($"[WebGL] Missing {FontAsset}"); return; }

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            if (source == null) { Debug.LogError($"[WebGL] Missing {SourceFont}"); return; }

            long before = new FileInfo(FontAsset).Length;
            int glyphsBefore = font.glyphTable?.Count ?? 0;

            var serialized = new SerializedObject(font);
            // Static assets store no live reference to the font they were baked from,
            // so dynamic mode has nothing to rasterise until this is put back.
            serialized.FindProperty("m_SourceFontFile").objectReferenceValue = source;
            serialized.FindProperty("m_AtlasPopulationMode").enumValueIndex = (int)AtlasPopulationMode.Dynamic;
            // Keeps the baked glyphs from being written back into the asset by a
            // play session, which would undo this over time.
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            serialized.ApplyModifiedProperties();

            // Drops the baked atlas pages and glyph tables. Without this the asset
            // keeps shipping the 4096x4096 page it no longer needs.
            font.ClearFontAssetData(true);

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAsset, ImportAssetOptions.ForceUpdate);

            long after = new FileInfo(FontAsset).Length;
            Debug.Log($"[WebGL] Font: {Path.GetFileName(FontAsset)} " +
                      $"{before:N0} -> {after:N0} bytes on disk, " +
                      $"glyph table {glyphsBefore:N0} -> {font.glyphTable?.Count ?? 0:N0}, " +
                      $"mode={font.atlasPopulationMode}, source={source.name}");
        }
    }
}
