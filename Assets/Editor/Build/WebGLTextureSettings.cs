using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClayWars.Build
{
    /// <summary>
    /// Applies the WebGL texture import overrides measured in the build size work.
    ///
    /// Every texture already carries a WebGL platform entry, but with
    /// overridden: 0, so the WebGL build inherits the desktop defaults. The
    /// stages below are deliberately separate: each one is built and measured on
    /// its own, so a size change can be attributed to a single setting.
    ///
    /// Invoke one stage per run from the Unity CLI, for example:
    ///   --execute-method ClayWars.Build.WebGLTextureSettings.ApplyCompressionFix
    /// Each stage writes Logs/webgl-texture-&lt;stage&gt;.tsv listing every before and
    /// after value it changed.
    /// </summary>
    public static class WebGLTextureSettings
    {
        public const string Platform = "WebGL";

        /// <summary>Per-folder WebGL ceilings for the max-size stage; longest matching prefix wins.</summary>
        private static readonly (string Prefix, int MaxSize)[] SizeRules =
        {
            ("Assets/", 1024),
            // This pack alone inherits 4096 while the rest of the project is 2048,
            // which is why its three textures were 43.0 MB of a 276.5 MB build.
            // 1024 is already a 16x reduction; the prop is visible in Battle.unity,
            // so going lower is not worth the risk without seeing it first.
            ("Assets/GameAssets/Stylized Rock - Desert style free/", 1024),
            // Full-screen UI art is where the player sees texels closest to 1:1.
            ("Assets/OutGame/Arts/2D/", 2048),
        };

        /// <summary>
        /// Stage 1. Per-folder maximum sizes. Nothing in the build ships
        /// uncompressed, so resolution is the first real lever: one asset pack
        /// inherits maxTextureSize 4096 while the rest of the project is 2048.
        /// </summary>
        [MenuItem("ClayWars/WebGL/1 - Apply max sizes")]
        public static void ApplyMaxSizes() => Apply(Stage.MaxSizes);

        /// <summary>Stage 2. Crunch on project textures, resolution unchanged.</summary>
        [MenuItem("ClayWars/WebGL/2 - Apply Crunch compression")]
        public static void ApplyCrunch() => Apply(Stage.Crunch);

        /// <summary>Removes every WebGL override this tool applied.</summary>
        [MenuItem("ClayWars/WebGL/Clear WebGL texture overrides")]
        public static void Clear() => Apply(Stage.Clear);

        private enum Stage { MaxSizes, Crunch, Clear }

        private static void Apply(Stage stage)
        {
            var changed = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!ShouldTouch(path)) continue;
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(Platform);
                    // The WebGL entry stores a stale value when it is not overridden,
                    // so the resolution this texture actually builds at comes from the
                    // default platform settings, not from the WebGL entry.
                    int inherited = importer.GetDefaultPlatformTextureSettings().maxTextureSize;
                    string before = Describe(settings) + $" inherited={inherited}";

                    switch (stage)
                    {
                        case Stage.Clear:
                            settings.overridden = false;
                            break;
                        case Stage.Crunch:
                            // Turning the override on must not move the resolution,
                            // so pin it to whatever this texture resolves to today.
                            if (!settings.overridden) settings.maxTextureSize = inherited;
                            settings.overridden = true;
                            settings.crunchedCompression = true;
                            settings.compressionQuality = 50;
                            if (settings.textureCompression == TextureImporterCompression.CompressedHQ)
                                settings.textureCompression = TextureImporterCompression.Compressed;
                            break;
                        case Stage.MaxSizes:
                            // A ceiling, never an assignment. Some art is already
                            // imported far below these numbers on purpose - four UI
                            // icons sit at 256 - and writing the rule value straight
                            // in silently blew them up 16x.
                            int capped = Math.Min(inherited, MaxSizeFor(path));
                            if (capped >= inherited) continue;
                            settings.overridden = true;
                            settings.maxTextureSize = capped;
                            break;
                    }

                    string after = Describe(settings) + $" inherited={inherited}";
                    if (after == before) continue;
                    importer.SetPlatformTextureSettings(settings);
                    changed.Add($"{path}\t{before}\t{after}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (string line in changed)
                AssetDatabase.ImportAsset(line.Split('\t')[0], ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            WriteLog(stage.ToString(), changed);
            Debug.Log($"[WebGL] {stage}: updated {changed.Count} of {guids.Length} textures.");
        }

        /// <summary>Project art only: packages and the TMP sample set stay untouched.</summary>
        public static bool ShouldTouch(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                return false;
            if (path.StartsWith("Assets/TextMesh Pro/", StringComparison.Ordinal)) return false;
            if (path.StartsWith("Assets/WebGLTemplates/", StringComparison.Ordinal)) return false;
            return true;
        }

        public static int MaxSizeFor(string path)
        {
            int size = 1024;
            int bestLength = -1;
            foreach ((string prefix, int maxSize) in SizeRules)
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (prefix.Length <= bestLength) continue;
                bestLength = prefix.Length;
                size = maxSize;
            }
            return size;
        }

        private static string Describe(TextureImporterPlatformSettings settings) =>
            string.Format(CultureInfo.InvariantCulture,
                "overridden={0} max={1} compression={2} crunch={3} quality={4}",
                settings.overridden, settings.maxTextureSize, settings.textureCompression,
                settings.crunchedCompression, settings.compressionQuality);

        private static void WriteLog(string stage, List<string> changed)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(root, "Logs");
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, $"webgl-texture-{stage}.tsv");
            File.WriteAllLines(file, new[] { "path\tbefore\tafter" }.Concat(changed));
            Debug.Log("[WebGL] Wrote " + file);
        }
    }
}
