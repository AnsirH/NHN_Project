using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ClayWars.Build
{
    public static class WebGLBuilder
    {
        // Shared entry point for local Unity CLI builds and GitHub Actions.
        public static void Build()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var output = ResolveOutput(projectRoot, Environment.GetCommandLineArgs());
            var scenes = EnabledScenes(EditorBuildSettings.scenes);
            foreach (var scene in scenes)
            {
                if (!File.Exists(Path.Combine(projectRoot, scene)))
                    throw new FileNotFoundException("Enabled build scene is missing.", scene);
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install Web Build Support for " + Application.unityVersion + ".");

            WithWebGLSettings(() =>
            {
                PrepareOutput(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.DetailedBuildReport
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("WebGL build failed: " + report.summary.result);

                File.WriteAllText(Path.Combine(output, "build-info.json"), JsonUtility.ToJson(new BuildInfo
                {
                    editorVersion = Application.unityVersion,
                    revision = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local",
                    builtAtUtc = DateTime.UtcNow.ToString("O")
                }, true));
                Debug.Log("[WebGL] Built " + report.summary.totalSize + " bytes at " + output);
            });
        }

        public static string ResolveOutput(string projectRoot, string[] arguments)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Project root is required.");
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            string output = Path.Combine("Build", "WebGL");
            bool found = false;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] != "-buildOutput") continue;
                if (found || i + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[i + 1]) || arguments[i + 1].StartsWith("-"))
                    throw new ArgumentException("Provide exactly one non-empty -buildOutput value.");
                output = arguments[++i];
                found = true;
            }

            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, output));
            string buildRoot = Path.GetFullPath(Path.Combine(projectRoot, "Build")) + Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(buildRoot, comparison))
                throw new ArgumentException("Build output must be inside the project's Build directory.");
            return fullPath;
        }

        public static string[] EnabledScenes(EditorBuildSettingsScene[] scenes)
        {
            if (scenes == null) throw new ArgumentNullException(nameof(scenes));
            string[] enabled = scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (enabled.Length == 0) throw new InvalidOperationException("No enabled build scenes.");
            return enabled;
        }

        public static void WithWebGLSettings(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            var memory = PlayerSettings.WebGL.initialMemorySize;
            var hashes = PlayerSettings.WebGL.nameFilesAsHashes;
            var template = PlayerSettings.WebGL.template;
            try
            {
                PlayerSettings.WebGL.compressionFormat = CompressionFormat();
                PlayerSettings.WebGL.decompressionFallback = DecompressionFallback();
                PlayerSettings.WebGL.initialMemorySize = 256;
                PlayerSettings.WebGL.nameFilesAsHashes = true;
                PlayerSettings.WebGL.template = "PROJECT:ClayWarsFill";
                action();
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.initialMemorySize = memory;
                PlayerSettings.WebGL.nameFilesAsHashes = hashes;
                PlayerSettings.WebGL.template = template;
            }
        }

        // GitHub Pages cannot add a Content-Encoding header, so which compression
        // format and fallback combination is actually fastest there has to be
        // measured rather than assumed. These two overrides let a run pick a
        // combination without editing this file; CI sets neither, so the shipped
        // configuration is exactly what it was.
        private const string CompressionVariable = "CLAYWARS_WEBGL_COMPRESSION";
        private const string FallbackVariable = "CLAYWARS_WEBGL_DECOMPRESSION_FALLBACK";

        private static WebGLCompressionFormat CompressionFormat()
        {
            string value = Environment.GetEnvironmentVariable(CompressionVariable);
            // Brotli over Gzip, measured on the Pages replica: it moves 8.5 MB less
            // and costs 1.7 s more to unpack in JavaScript, which pays off below
            // about 41 Mbit/s. Phone browsers are the target and the unpacking is
            // paid once, since the build files are cached as immutable.
            if (string.IsNullOrWhiteSpace(value)) return WebGLCompressionFormat.Brotli;
            if (!Enum.TryParse(value, true, out WebGLCompressionFormat parsed))
                throw new ArgumentException(
                    $"{CompressionVariable} must be Brotli, Gzip or Disabled, not '{value}'.");
            return parsed;
        }

        private static bool DecompressionFallback()
        {
            string value = Environment.GetEnvironmentVariable(FallbackVariable);
            if (string.IsNullOrWhiteSpace(value)) return true;
            if (!bool.TryParse(value, out bool parsed))
                throw new ArgumentException(
                    $"{FallbackVariable} must be true or false, not '{value}'.");
            return parsed;
        }

        private static void PrepareOutput(string output)
        {
            // ResolveOutput already restricts this path to Build/<directory>.
            // Reject junctions/symlinks before recursively removing generated files.
            for (var ancestor = new DirectoryInfo(output); ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Build output cannot traverse a junction or symbolic link.");
            }
            if (Directory.Exists(output))
            {
                var pending = new Stack<string>();
                pending.Push(output);
                while (pending.Count > 0)
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            throw new IOException("Build output contains a junction or symbolic link.");
                        if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    }
                }
                Directory.Delete(output, true);
            }
            Directory.CreateDirectory(output);
        }

        [Serializable]
        private sealed class BuildInfo
        {
            public string editorVersion;
            public string revision;
            public string builtAtUtc;
        }
    }
}
