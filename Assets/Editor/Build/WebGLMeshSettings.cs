using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClayWars.Build
{
    /// <summary>
    /// Sets mesh compression on the project's models. Unlike the texture settings
    /// this is not per platform - ModelImporter has one meshCompression - so the
    /// value applies everywhere, and the before/after captures are what decides
    /// whether the quantisation shows.
    /// </summary>
    public static class WebGLMeshSettings
    {
        [MenuItem("ClayWars/WebGL/Mesh - compress Medium")]
        public static void ApplyMedium() => Apply(ModelImporterMeshCompression.Medium);

        [MenuItem("ClayWars/WebGL/Mesh - compression Off")]
        public static void ApplyOff() => Apply(ModelImporterMeshCompression.Off);

        private static void Apply(ModelImporterMeshCompression level)
        {
            string[] guids = AssetDatabase.FindAssets("t:Model");
            var log = new List<string> { "path\tbefore\tafter" };
            var changed = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith("Packages/")) continue;
                    if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                    if (importer.meshCompression == level) continue;

                    log.Add($"{path}\t{importer.meshCompression}\t{level}");
                    importer.meshCompression = level;
                    changed.Add(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (string path in changed)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            string file = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", $"webgl-mesh-{level}.tsv");
            File.WriteAllLines(file, log);
            Debug.Log($"[WebGL] Mesh {level}: updated {changed.Count} of {guids.Length} models. Log: {file}");
        }
    }
}
