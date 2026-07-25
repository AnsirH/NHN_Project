using System;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>적 군대 구성 자동 생성 검증 (§4-28): 일반전투는 층별 스케일링, 보스는 고정 구성.</summary>
    public class EnemyCompositionGeneratorTests
    {
        [Test]
        public void Generate_Boss_ReturnsFixedComposition()
        {
            var config = new EnemyCompositionConfig();
            var result = EnemyCompositionGenerator.Generate(9, RoomType.Boss, config, new Random(1));

            CollectionAssert.AreEqual(config.bossComposition, result);
        }

        [Test]
        public void Generate_Boss_IgnoresFloor()
        {
            var config = new EnemyCompositionConfig();
            var atFloor0 = EnemyCompositionGenerator.Generate(0, RoomType.Boss, config, new Random(1));
            var atFloor9 = EnemyCompositionGenerator.Generate(9, RoomType.Boss, config, new Random(1));

            CollectionAssert.AreEqual(atFloor0, atFloor9);
        }

        [Test]
        public void Generate_NormalBattle_FloorZero_UsesBaseCount()
        {
            var config = new EnemyCompositionConfig { baseEnemyCount = 3, perFloorEnemyIncrement = 1, maxEnemyCount = 20 };
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, new Random(1));

            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void Generate_NormalBattle_ScalesCountWithFloor()
        {
            var config = new EnemyCompositionConfig { baseEnemyCount = 3, perFloorEnemyIncrement = 1, maxEnemyCount = 20 };
            var result = EnemyCompositionGenerator.Generate(4, RoomType.NormalBattle, config, new Random(1));

            Assert.AreEqual(7, result.Count); // 3 + 4×1
        }

        [Test]
        public void Generate_NormalBattle_CapsAtMaxEnemyCount()
        {
            var config = new EnemyCompositionConfig { baseEnemyCount = 3, perFloorEnemyIncrement = 5, maxEnemyCount = 9 };
            var result = EnemyCompositionGenerator.Generate(9, RoomType.NormalBattle, config, new Random(1));

            Assert.AreEqual(9, result.Count);
        }

        [Test]
        public void Generate_ZeroClassWeights_OnlyProducesBaseClass()
        {
            var config = new EnemyCompositionConfig
            {
                baseEnemyCount = 5, perFloorEnemyIncrement = 0, maxEnemyCount = 5,
                baseWeight = 1f, archerWeight = 0f, shieldmanWeight = 0f,
            };
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, new Random(1));

            Assert.IsTrue(result.TrueForAll(c => c == ArmyClass.None));
        }

        [Test]
        public void Generate_NegativeFloor_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                EnemyCompositionGenerator.Generate(-1, RoomType.NormalBattle, new EnemyCompositionConfig(), new Random(1)));
        }

        [Test]
        public void Generate_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, new EnemyCompositionConfig(), null));
        }

        [Test]
        public void Validate_DefaultConfig_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new EnemyCompositionConfig().Validate());
        }

        [Test]
        public void Validate_MaxBelowBase_Throws()
        {
            var config = new EnemyCompositionConfig { baseEnemyCount = 10, maxEnemyCount = 5 };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_EmptyBossComposition_Throws()
        {
            var config = new EnemyCompositionConfig { bossComposition = new System.Collections.Generic.List<ArmyClass>() };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }
    }
}
