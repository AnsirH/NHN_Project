using System;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 적 군대 구성 자동 생성 검증 (§4-28): 일반전투는 층별 스케일링, 보스는 고정 구성.
    /// 각 결과는 플레이어 군대와 같은 형태(ArmyDefinition 참조+병과+병사 수)를 가져야 한다.
    /// </summary>
    public class EnemyCompositionGeneratorTests
    {
        private static ArmyData Template(int baseSoldierCount = 30, float generalPower = 10f) =>
            new ArmyData { id = "army_basic", baseSoldierCount = baseSoldierCount, generalPower = generalPower };

        [Test]
        public void Generate_Boss_ReturnsFixedComposition()
        {
            var config = new EnemyCompositionConfig();
            var result = EnemyCompositionGenerator.Generate(9, RoomType.Boss, config, Template(), new Random(1));

            CollectionAssert.AreEqual(config.bossComposition, result.Select(e => e.armyClass).ToList());
        }

        [Test]
        public void Generate_Boss_IgnoresFloor()
        {
            var config = new EnemyCompositionConfig();
            var atFloor0 = EnemyCompositionGenerator.Generate(0, RoomType.Boss, config, Template(), new Random(1));
            var atFloor9 = EnemyCompositionGenerator.Generate(9, RoomType.Boss, config, Template(), new Random(1));

            CollectionAssert.AreEqual(atFloor0.Select(e => e.armyClass), atFloor9.Select(e => e.armyClass));
        }

        [Test]
        public void Generate_EachEntry_InheritsTemplateStats()
        {
            var config = new EnemyCompositionConfig { baseEnemyCount = 3, perFloorEnemyIncrement = 0, maxEnemyCount = 3 };
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, Template(baseSoldierCount: 42), new Random(1));

            Assert.IsTrue(result.TrueForAll(e => e.armyDefId == "army_basic"));
            Assert.IsTrue(result.TrueForAll(e => e.soldierCount == 42), "각 항목은 template의 baseSoldierCount를 그대로 물려받아야 함");
        }

        [Test]
        public void Generate_NormalBattle_FloorZero_UsesBaseCount()
        {
            var config = new EnemyCompositionConfig { baseEnemyCount = 3, perFloorEnemyIncrement = 1, maxEnemyCount = 20 };
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, Template(), new Random(1));

            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void Generate_NormalBattle_ScalesCountWithFloor()
        {
            var config = new EnemyCompositionConfig { baseEnemyCount = 3, perFloorEnemyIncrement = 1, maxEnemyCount = 20 };
            var result = EnemyCompositionGenerator.Generate(4, RoomType.NormalBattle, config, Template(), new Random(1));

            Assert.AreEqual(7, result.Count); // 3 + 4×1
        }

        [Test]
        public void Generate_NormalBattle_CapsAtMaxEnemyCount()
        {
            var config = new EnemyCompositionConfig { baseEnemyCount = 3, perFloorEnemyIncrement = 5, maxEnemyCount = 9 };
            var result = EnemyCompositionGenerator.Generate(9, RoomType.NormalBattle, config, Template(), new Random(1));

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
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, Template(), new Random(1));

            Assert.IsTrue(result.TrueForAll(e => e.armyClass == ArmyClass.None));
        }

        [Test]
        public void Generate_NegativeFloor_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                EnemyCompositionGenerator.Generate(-1, RoomType.NormalBattle, new EnemyCompositionConfig(), Template(), new Random(1)));
        }

        [Test]
        public void Generate_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, null, Template(), new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, new EnemyCompositionConfig(), null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, new EnemyCompositionConfig(), Template(), null));
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

        [Test]
        public void Clone_ReturnsIndependentCopy()
        {
            var original = new EnemyCompositionConfig { baseEnemyCount = 5 };
            EnemyCompositionConfig clone = original.Clone();
            clone.baseEnemyCount = 99;
            clone.bossComposition.Add(ArmyClass.Archer);

            Assert.AreEqual(5, original.baseEnemyCount, "clone 변형이 원본에 영향을 주면 안 됨");
            Assert.AreNotEqual(original.bossComposition.Count, clone.bossComposition.Count, "리스트도 독립 복사돼야 함");
        }

        [Test]
        public void EnemyArmy_Validate_EmptyArmyDefId_Throws()
        {
            var enemy = new EnemyArmy { armyDefId = "", armyClass = ArmyClass.Archer, soldierCount = 10 };
            Assert.Throws<ArgumentException>(() => enemy.Validate());
        }

        [Test]
        public void EnemyArmy_Validate_NegativeSoldierCount_Throws()
        {
            var enemy = new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.Archer, soldierCount = -1 };
            Assert.Throws<ArgumentException>(() => enemy.Validate());
        }

        [Test]
        public void EnemyArmy_Validate_ValidData_DoesNotThrow()
        {
            var enemy = new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.Archer, soldierCount = 10 };
            Assert.DoesNotThrow(() => enemy.Validate());
        }
    }
}
