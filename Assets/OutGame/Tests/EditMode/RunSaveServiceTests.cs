using System;
using System.IO;
using NUnit.Framework;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>런 저장/이어하기 파일 I/O 검증 (§6, §5.1).</summary>
    public class RunSaveServiceTests
    {
        private string tempPath;

        private static RunState NewRun()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), 7).Generate();
            return RunStateFactory.Create(map, new RunConfig());
        }

        [SetUp]
        public void SetUp() => tempPath = Path.Combine(Path.GetTempPath(), $"runsave_test_{Guid.NewGuid():N}.json");

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        [Test]
        public void Save_Then_Load_RoundTrips()
        {
            RunState run = NewRun();
            run.gold = 42;

            RunSaveService.Save(run, tempPath);
            RunState loaded = RunSaveService.Load(tempPath);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(42, loaded.gold);
            Assert.AreEqual(run.armies.Count, loaded.armies.Count);
        }

        [Test]
        public void HasSave_ReflectsFileExistence()
        {
            Assert.IsFalse(RunSaveService.HasSave(tempPath));
            RunSaveService.Save(NewRun(), tempPath);
            Assert.IsTrue(RunSaveService.HasSave(tempPath));
        }

        [Test]
        public void Load_NonexistentFile_ReturnsNull()
        {
            Assert.IsNull(RunSaveService.Load(tempPath));
        }

        [Test]
        public void Load_CorruptFile_ReturnsNull()
        {
            File.WriteAllText(tempPath, "not valid json");
            Assert.IsNull(RunSaveService.Load(tempPath));
        }

        [Test]
        public void DeleteSave_RemovesFile()
        {
            RunSaveService.Save(NewRun(), tempPath);
            RunSaveService.DeleteSave(tempPath);
            Assert.IsFalse(File.Exists(tempPath));
        }

        [Test]
        public void DeleteSave_NonexistentFile_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RunSaveService.DeleteSave(tempPath));
        }

        [Test]
        public void Save_NullRun_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RunSaveService.Save(null, tempPath));
        }

        [Test]
        public void Save_EmptyPath_Throws()
        {
            Assert.Throws<ArgumentException>(() => RunSaveService.Save(NewRun(), ""));
        }

        [Test]
        public void DefaultPath_IsUnderPersistentDataPath()
        {
            StringAssert.StartsWith(UnityEngine.Application.persistentDataPath, RunSaveService.DefaultPath);
        }

        [Test]
        public void Save_OverwritesPreviousContentAndLeavesNoTempFile()
        {
            RunState first = NewRun();
            first.gold = 1;
            RunSaveService.Save(first, tempPath);

            RunState second = NewRun();
            second.gold = 2;
            RunSaveService.Save(second, tempPath);

            RunState loaded = RunSaveService.Load(tempPath);
            Assert.AreEqual(2, loaded.gold);
            Assert.IsFalse(File.Exists(tempPath + ".tmp"), "저장 후 임시 파일이 남아 있으면 안 됨");
        }

        [Test]
        public void Save_CreatesMissingParentDirectory()
        {
            string nestedPath = Path.Combine(Path.GetTempPath(), $"runsave_dir_{Guid.NewGuid():N}", "run.json");
            try
            {
                Assert.DoesNotThrow(() => RunSaveService.Save(NewRun(), nestedPath));
                Assert.IsTrue(File.Exists(nestedPath));
            }
            finally
            {
                string dir = Path.GetDirectoryName(nestedPath);
                if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }
    }
}
