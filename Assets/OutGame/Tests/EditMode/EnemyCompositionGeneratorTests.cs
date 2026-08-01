using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 적 군대 구성 자동 생성 검증 (§4-28, 2026-07-26 재설계): powerRoomsVisited(난이도 커브
    /// 기준점)로 DifficultyTier를 골라 일반전투 개수/병과 가중치, 보스 고정 구성을 결정한다.
    /// 각 결과는 플레이어 군대와 같은 형태(ArmyDefinition 참조+병과+병사 수)를 가져야 한다.
    /// </summary>
    public class EnemyCompositionGeneratorTests
    {
        private static ArmyData Template(int baseSoldierCount = 30, float generalPower = 10f) =>
            new ArmyData { id = "army_basic", baseSoldierCount = baseSoldierCount, generalPower = generalPower };

        private static EnemyCompositionConfig SingleTierConfig(DifficultyTier tier) =>
            new EnemyCompositionConfig { tiers = new List<DifficultyTier> { tier } };

        [Test]
        public void Generate_Boss_ReturnsFixedCompositionOfMatchingTier()
        {
            var tier = new DifficultyTier
            {
                minCounter = 0, enemyCount = 1,
                bossComposition = new List<ArmyClass> { ArmyClass.Warrior, ArmyClass.Archer },
            };
            var config = SingleTierConfig(tier);
            var result = EnemyCompositionGenerator.Generate(0, RoomType.Boss, config, Template(), new Random(1));

            CollectionAssert.AreEqual(tier.bossComposition, result.Select(e => e.armyClass).ToList());
        }

        [Test]
        public void Generate_Boss_HigherCounter_UsesHigherTierComposition()
        {
            // 2026-07-26: 보스 구성도 카운터(티어)에 반응해야 한다 — 사용자 확정.
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 0, enemyCount = 1, bossComposition = new List<ArmyClass> { ArmyClass.None } },
                    new DifficultyTier { minCounter = 5, enemyCount = 1, bossComposition = new List<ArmyClass> { ArmyClass.Assassin } },
                },
            };

            var early = EnemyCompositionGenerator.Generate(0, RoomType.Boss, config, Template(), new Random(1));
            var late = EnemyCompositionGenerator.Generate(5, RoomType.Boss, config, Template(), new Random(1));

            CollectionAssert.AreEqual(new[] { ArmyClass.None }, early.Select(e => e.armyClass).ToList());
            CollectionAssert.AreEqual(new[] { ArmyClass.Assassin }, late.Select(e => e.armyClass).ToList());
        }

        [Test]
        public void Generate_EachEntry_InheritsTemplateStats()
        {
            var config = SingleTierConfig(new DifficultyTier { minCounter = 0, enemyCount = 3 });
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, Template(baseSoldierCount: 42), new Random(1));

            Assert.IsTrue(result.TrueForAll(e => e.armyDefId == "army_basic"));
            Assert.IsTrue(result.TrueForAll(e => e.soldierCount == 42), "각 항목은 template의 baseSoldierCount를 그대로 물려받아야 함");
        }

        [Test]
        public void Generate_EachEntry_CopiesTemplateFinalStats()
        {
            // 2026-08-02: 적도 아군(DeployedArmy)과 대칭으로 최종 스탯을 계약에 실어 보낸다 —
            // 지금은 배율 없이 template(ArmyDefinition_Basic) 원본값을 그대로 옮긴다.
            var template = new ArmyData
            {
                id = "army_basic",
                generalHealth = 111f, generalAttack = 22f, generalDefense = 33f,
                generalCritRate = 7f, generalMoveSpeed = 88f,
                soldierHealth = 44f, soldierAttack = 5f, soldierDefense = 6f,
            };
            var config = SingleTierConfig(new DifficultyTier { minCounter = 0, enemyCount = 3 });
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, template, new Random(1));

            Assert.IsTrue(result.TrueForAll(e => e.generalHealth == 111f));
            Assert.IsTrue(result.TrueForAll(e => e.generalAttack == 22f));
            Assert.IsTrue(result.TrueForAll(e => e.generalDefense == 33f));
            Assert.IsTrue(result.TrueForAll(e => e.generalCritRate == 7f));
            Assert.IsTrue(result.TrueForAll(e => e.generalMoveSpeed == 88f));
            Assert.IsTrue(result.TrueForAll(e => e.soldierHealth == 44f));
            Assert.IsTrue(result.TrueForAll(e => e.soldierAttack == 5f));
            Assert.IsTrue(result.TrueForAll(e => e.soldierDefense == 6f));
        }

        [Test]
        public void Generate_NormalBattle_UsesMatchingTierEnemyCount()
        {
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 0, enemyCount = 3 },
                    new DifficultyTier { minCounter = 4, enemyCount = 7 },
                },
            };

            var lowCounter = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, Template(), new Random(1));
            var highCounter = EnemyCompositionGenerator.Generate(4, RoomType.NormalBattle, config, Template(), new Random(1));

            Assert.AreEqual(3, lowCounter.Count);
            Assert.AreEqual(7, highCounter.Count);
        }

        [Test]
        public void Generate_CounterBelowFirstTier_ClampsToFirstTier()
        {
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier> { new DifficultyTier { minCounter = 2, enemyCount = 5 } },
            };

            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, Template(), new Random(1));

            Assert.AreEqual(5, result.Count, "첫 구간의 minCounter보다 낮아도 첫 구간으로 클램프돼야 함");
        }

        [Test]
        public void Generate_ZeroClassWeights_OnlyProducesBaseClass()
        {
            var config = SingleTierConfig(new DifficultyTier
            {
                minCounter = 0, enemyCount = 5,
                baseWeight = 1f, archerWeight = 0f, warriorWeight = 0f, hunterWeight = 0f, assassinWeight = 0f,
            });
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, Template(), new Random(1));

            Assert.IsTrue(result.TrueForAll(e => e.armyClass == ArmyClass.None));
        }

        [Test]
        public void Generate_AllWeightsPositive_CanProduceEveryClass()
        {
            // 최종 티어(전부 해금)에서 5개 병과가 전부 나올 수 있는지 결정론적으로 확인 — 표본을
            // 충분히 뽑아 병과별 최소 1회 이상 등장을 기대한다(시드 고정, 낮은 확률로도 실패하지
            // 않도록 표본 수를 넉넉히 잡음).
            var config = SingleTierConfig(new DifficultyTier
            {
                minCounter = 0, enemyCount = 200,
                baseWeight = 1f, archerWeight = 1f, warriorWeight = 1f, hunterWeight = 1f, assassinWeight = 1f,
            });
            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, Template(), new Random(1));

            var producedClasses = result.Select(e => e.armyClass).Distinct().ToList();
            CollectionAssert.AreEquivalent(
                new[] { ArmyClass.None, ArmyClass.Archer, ArmyClass.Warrior, ArmyClass.Hunter, ArmyClass.Assassin },
                producedClasses);
        }

        [Test]
        public void Generate_NegativeCounter_Throws()
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
        public void Validate_EmptyTiers_Throws()
        {
            var config = new EnemyCompositionConfig { tiers = new List<DifficultyTier>() };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_TiersNotStrictlyAscending_Throws()
        {
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 2, enemyCount = 1 },
                    new DifficultyTier { minCounter = 2, enemyCount = 1 },
                },
            };
            Assert.Throws<ArgumentException>(() => config.Validate(), "minCounter 중복은 허용하면 안 됨");
        }

        [Test]
        public void Validate_TierWithEmptyBossComposition_Throws()
        {
            var config = SingleTierConfig(new DifficultyTier { minCounter = 0, enemyCount = 1, bossComposition = new List<ArmyClass>() });
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void GetTierFor_ExactBoundary_UsesThatTier()
        {
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 0, enemyCount = 1 },
                    new DifficultyTier { minCounter = 3, enemyCount = 2 },
                    new DifficultyTier { minCounter = 6, enemyCount = 3 },
                },
            };

            Assert.AreEqual(1, config.GetTierFor(2).enemyCount);
            Assert.AreEqual(2, config.GetTierFor(3).enemyCount, "경계값(minCounter와 정확히 같음)은 그 구간을 써야 함");
            Assert.AreEqual(2, config.GetTierFor(5).enemyCount);
            Assert.AreEqual(3, config.GetTierFor(100).enemyCount, "마지막 구간을 넘어가면 마지막 구간 유지");
        }

        [Test]
        public void Clone_ReturnsIndependentCopy()
        {
            var original = new EnemyCompositionConfig();
            EnemyCompositionConfig clone = original.Clone();
            clone.tiers[0].enemyCount = 999;
            clone.tiers[0].bossComposition.Add(ArmyClass.Archer);

            Assert.AreNotEqual(999, original.tiers[0].enemyCount, "clone 변형이 원본에 영향을 주면 안 됨");
            Assert.AreNotEqual(original.tiers[0].bossComposition.Count, clone.tiers[0].bossComposition.Count,
                "리스트도 독립 복사돼야 함");
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
