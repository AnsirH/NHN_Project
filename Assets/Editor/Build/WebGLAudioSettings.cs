using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClayWars.Build
{
    /// <summary>
    /// Adjusts the WebGL audio import settings, one stage at a time so each size
    /// or memory change can be attributed to a single setting.
    ///
    /// Music and effects are treated separately: the three music tracks are 13.6 MB
    /// of a 13.7 MB sound budget, and the effects are a few kilobytes each, where
    /// streaming would cost more than it saves.
    /// </summary>
    public static class WebGLAudioSettings
    {
        public const string Platform = "WebGL";
        private const string MusicFolder = "Assets/Music/";

        private enum Stage { Quality, StreamMusic, Clear }

        [MenuItem("ClayWars/WebGL/Audio - lower music quality")]
        public static void ApplyQuality() => Apply(Stage.Quality);

        [MenuItem("ClayWars/WebGL/Audio - stream music")]
        public static void ApplyStreaming() => Apply(Stage.StreamMusic);

        [MenuItem("ClayWars/WebGL/Audio - clear WebGL overrides")]
        public static void Clear() => Apply(Stage.Clear);

        private static void Apply(Stage stage)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip");
            var log = new List<string> { "path\tbefore\tafter" };
            int changed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

                    bool isMusic = path.StartsWith(MusicFolder, StringComparison.Ordinal);
                    AudioImporterSampleSettings settings = importer.GetOverrideSampleSettings(Platform);
                    bool overridden = importer.ContainsSampleSettingsOverride(Platform);
                    string before = Describe(settings, overridden);

                    switch (stage)
                    {
                        case Stage.Clear:
                            if (!overridden) continue;
                            importer.ClearSampleSettingOverride(Platform);
                            log.Add($"{path}\t{before}\t{Describe(importer.defaultSampleSettings, false)}");
                            changed++;
                            continue;

                        case Stage.Quality:
                            // Vorbis at 1.0 is near lossless. Game music is mixed under
                            // effects and voice, so the top of that range buys nothing a
                            // player can hear while costing half the file.
                            if (!isMusic) continue;
                            settings.quality = 0.5f;
                            // The compression format is deliberately left alone. WebGL
                            // resolves to AAC by default, not to the Vorbis the meta's
                            // default-platform entry shows, and changing both at once
                            // would make the size difference unattributable.
                            break;

                        case Stage.StreamMusic:
                            // A 4.7 MB Vorbis track on DecompressOnLoad becomes tens of
                            // megabytes of PCM in the heap. That is the memory a phone
                            // browser kills the tab over, and streaming costs one buffer.
                            if (!isMusic) continue;
                            settings.loadType = AudioClipLoadType.Streaming;
                            break;
                    }

                    importer.SetOverrideSampleSettings(Platform, settings);
                    log.Add($"{path}\t{before}\t{Describe(settings, true)}");
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (string line in log.Skip(1))
                AssetDatabase.ImportAsset(line.Split('\t')[0], ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            string file = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", $"webgl-audio-{stage}.tsv");
            File.WriteAllLines(file, log);
            Debug.Log($"[WebGL] Audio {stage}: updated {changed} of {guids.Length} clips. Log: {file}");
        }

        private static string Describe(AudioImporterSampleSettings s, bool overridden) =>
            $"overridden={overridden} loadType={s.loadType} format={s.compressionFormat} " +
            $"quality={s.quality:0.00} sampleRate={s.sampleRateSetting}";
    }
}
