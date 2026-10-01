using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace ClayWars.Build.Tests
{
    public class WebGLBuilderTests
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ClayWarsBuildTests"));

        [Test]
        public void Output_DefaultsToProjectBuildDirectory()
        {
            Assert.That(WebGLBuilder.ResolveOutput(Root, Array.Empty<string>()),
                Is.EqualTo(Path.Combine(Root, "Build", "WebGL")));
        }

        [Test]
        public void Output_UsesUnityCliBuildOutputArgument()
        {
            Assert.That(WebGLBuilder.ResolveOutput(Root, new[] { "-projectPath", Root, "-buildOutput", "Build/Preview" }),
                Is.EqualTo(Path.Combine(Root, "Build", "Preview")));
        }

        [TestCase("Assets/WebGL")]
        [TestCase("Build/../Assets")]
        [TestCase(".")]
        [TestCase("../OtherProject")]
        public void Output_RejectsLocationsOutsideBuildDirectory(string path)
        {
            Assert.Throws<ArgumentException>(() => WebGLBuilder.ResolveOutput(Root, new[] { "-buildOutput", path }));
        }

        [Test]
        public void Output_RejectsMissingArgumentValue()
        {
            Assert.Throws<ArgumentException>(() => WebGLBuilder.ResolveOutput(Root, new[] { "-buildOutput" }));
        }

        [Test]
        public void Scenes_PreserveEnabledSceneOrderAndOmitDisabledScenes()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/OutGame/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/StressTest.unity", false),
                new EditorBuildSettingsScene("Assets/Scenes/Battle.unity", true)
            };
            Assert.That(WebGLBuilder.EnabledScenes(scenes), Is.EqualTo(new[]
                { "Assets/OutGame/Scenes/MainMenu.unity", "Assets/Scenes/Battle.unity" }));
        }

        [Test]
        public void Scenes_RejectEmptyEnabledSceneList()
        {
            Assert.Throws<InvalidOperationException>(() => WebGLBuilder.EnabledScenes(Array.Empty<EditorBuildSettingsScene>()));
        }

        [Test]
        public void Settings_ApplyPagesCompatibleCompressionAndRestoreAfterSuccess()
        {
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            var memory = PlayerSettings.WebGL.initialMemorySize;
            WebGLBuilder.WithWebGLSettings(() =>
            {
                Assert.That(PlayerSettings.WebGL.compressionFormat, Is.EqualTo(WebGLCompressionFormat.Gzip));
                Assert.That(PlayerSettings.WebGL.decompressionFallback, Is.True);
                Assert.That(PlayerSettings.WebGL.initialMemorySize, Is.EqualTo(256));
            });
            Assert.That(PlayerSettings.WebGL.compressionFormat, Is.EqualTo(compression));
            Assert.That(PlayerSettings.WebGL.decompressionFallback, Is.EqualTo(fallback));
            Assert.That(PlayerSettings.WebGL.initialMemorySize, Is.EqualTo(memory));
        }

        [Test]
        public void Settings_RestoreAfterBuildThrows()
        {
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            Assert.Throws<InvalidOperationException>(() => WebGLBuilder.WithWebGLSettings(() =>
                throw new InvalidOperationException("simulated build failure")));
            Assert.That(PlayerSettings.WebGL.compressionFormat, Is.EqualTo(compression));
            Assert.That(PlayerSettings.WebGL.decompressionFallback, Is.EqualTo(fallback));
        }
    }
}
