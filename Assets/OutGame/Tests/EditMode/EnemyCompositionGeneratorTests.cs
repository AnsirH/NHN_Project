using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 적 군대 구성 자동 생성 검증 (§4-28, 2026-08-02 프리셋 조각 기반 재설계): powerRoomsVisited
    /// (난이도 커브 기준점)로 DifficultyTier를 골라, 그 구간의 등급 가중치로 프리셋을 가중 랜덤
    /// 조합하거나(일반전투) 고정 프리셋 목록을 그대로 쓴다(보스). 뽑힌 프리셋들의 유닛을 합쳐
    /// 최종 스탯(ArmyStatCalculator, 아군과 동일 공식)을 계산한다.
    /// </summary>
    public class EnemyCompositionGeneratorTests
    {
        // 병과마다 같은 테스트용 스탯(100/5/2)을 준다 — 이 테스트는 "프리셋을 올바르게 조합·변환하는지"를
        // 검증하는 것이지 병과별 실제 밸런스 수치를 검증하는 게 아니므로, 값을 동일하게 둬서 어떤 병과가
        // 뽑혀도 기대값 계산이 단순해지게 한다.
        private static readonly Dictionary<string, ArmyData> ArmyDefs = new Dictionary<string, ArmyData>
        {
            ["army_none"] = new ArmyData { id = "army_none", soldierHealth = 100f, soldierAttack = 5f, soldierDefense = 2f },
            ["army_warrior"] = new ArmyData { id = "army_warrior", soldierHealth = 100f, soldierAttack = 5f, soldierDefense = 2f },
            ["army_archer"] = new ArmyData { id = "army_archer", soldierHealth = 100f, soldierAttack = 5f, soldierDefense = 2f },
            ["army_assassin"] = new ArmyData { id = "army_assassin", soldierHealth = 100f, soldierAttack = 5f, soldierDefense = 2f },
        };

        private static readonly Dictionary<string, AugmentData> NoAugmentData = new Dictionary<string, AugmentData>();

        private static EnemyPresetData Preset(string id, int grade, ArmyClass cls, int soldierCount = 30, int upgradeLevel = 0) =>
            new EnemyPresetData
            {
                presetId = id,
                displayName = id,
                grade = grade,
                units = new List<EnemyPresetUnit>
                {
                    new EnemyPresetUnit { armyClass = cls, soldierCount = soldierCount, upgradeLevel = upgradeLevel },
                },
            };

        private static Dictionary<string, EnemyPresetData> PresetsById(params EnemyPresetData[] presets) =>
            presets.ToDictionary(p => p.presetId);

        private static EnemyCompositionConfig SingleTierConfig(DifficultyTier tier) =>
            new EnemyCompositionConfig { tiers = new List<DifficultyTier> { tier } };

        [Test]
        public void Generate_Boss_ReturnsFixedCompositionOfMatchingTier()
        {
            var tier = new DifficultyTier { minCounter = 0, bossPresetIds = new List<string> { "p_warrior", "p_archer" } };
            var config = SingleTierConfig(tier);
            var presets = PresetsById(Preset("p_warrior", 1, ArmyClass.Warrior), Preset("p_archer", 1, ArmyClass.Archer));

            var result = EnemyCompositionGenerator.Generate(0, RoomType.Boss, config, presets, ArmyDefs, NoAugmentData, new Random(1));

            CollectionAssert.AreEqual(new[] { ArmyClass.Warrior, ArmyClass.Archer }, result.Select(e => e.armyClass).ToList());
        }

        [Test]
        public void Generate_Boss_HigherCounter_UsesHigherTierComposition()
        {
            // 2026-07-26: 보스 구성도 카운터(티어)에 반응해야 한다 — 사용자 확정.
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 0, bossPresetIds = new List<string> { "p_none" } },
                    new DifficultyTier { minCounter = 5, bossPresetIds = new List<string> { "p_assassin" } },
                },
            };
            var presets = PresetsById(Preset("p_none", 1, ArmyClass.None), Preset("p_assassin", 1, ArmyClass.Assassin));

            var early = EnemyCompositionGenerator.Generate(0, RoomType.Boss, config, presets, ArmyDefs, NoAugmentData, new Random(1));
            var late = EnemyCompositionGenerator.Generate(5, RoomType.Boss, config, presets, ArmyDefs, NoAugmentData, new Random(1));

            CollectionAssert.AreEqual(new[] { ArmyClass.None }, early.Select(e => e.armyClass).ToList());
            CollectionAssert.AreEqual(new[] { ArmyClass.Assassin }, late.Select(e => e.armyClass).ToList());
        }

        [Test]
        public void Generate_Boss_MissingPresetId_Throws()
        {
            var tier = new DifficultyTier { minCounter = 0, bossPresetIds = new List<string> { "p_ghost" } };
            var config = SingleTierConfig(tier);

            Assert.Throws<ArgumentException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.Boss, config, PresetsById(), ArmyDefs, NoAugmentData, new Random(1)));
        }

        [Test]
        public void Generate_EachEntry_InheritsPresetUnitFields()
        {
            // 단일 등급 풀에 프리셋 1개만 있으면 presetCount번 뽑아도 항상 같은 프리셋 — 결정론적 검증.
            var tier = new DifficultyTier
            {
                minCounter = 0, presetCount = 3,
                gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 1, weight = 1f } },
                bossPresetIds = new List<string> { "unused" },
            };
            var config = SingleTierConfig(tier);
            var presets = PresetsById(Preset("p_archer", 1, ArmyClass.Archer, soldierCount: 42));

            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1));

            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.TrueForAll(e => e.armyDefId == "army_archer"));
            Assert.IsTrue(result.TrueForAll(e => e.soldierCount == 42));
            Assert.IsTrue(result.TrueForAll(e => e.armyClass == ArmyClass.Archer));
        }

        [Test]
        public void Generate_UpgradeLevel_ScalesStatsLikeAlly()
        {
            var tier = new DifficultyTier
            {
                minCounter = 0, presetCount = 1,
                gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 1, weight = 1f } },
                bossPresetIds = new List<string> { "unused" },
            };
            var config = SingleTierConfig(tier);
            // None 병과라 병과 보너스 0 — 순수 업그레이드 배율(레벨2 → +20%)만 검증.
            var presets = PresetsById(Preset("p_none", 1, ArmyClass.None, upgradeLevel: 2));

            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1));

            Assert.AreEqual(120f, result[0].soldierHealth, 1e-3f, "레벨2 → +20%(ArmyUpgradeService와 동일 공식)");
            Assert.AreEqual(2, result[0].upgradeLevel, "참고용 upgradeLevel이 그대로 기록되어야 함");
        }

        [Test]
        public void Generate_MissingClassArmyDefinition_Throws()
        {
            // ArmyDefs 풀에 army_hunter가 없는 상태에서 Hunter 병과 프리셋을 뽑으면 예외가 나야 한다 —
            // 2026-08-02부터 armyDefId는 armyClass에서 자동 파생되므로(ClassArmyDefinitions), "정의
            // 없음"은 이제 풀에 그 병과의 ArmyDefinition 자체가 없을 때만 발생한다.
            var tier = new DifficultyTier { minCounter = 0, bossPresetIds = new List<string> { "p_hunter" } };
            var config = SingleTierConfig(tier);
            var presets = PresetsById(Preset("p_hunter", 1, ArmyClass.Hunter));

            Assert.Throws<ArgumentException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.Boss, config, presets, ArmyDefs, NoAugmentData, new Random(1)));
        }

        [Test]
        public void Generate_NormalBattle_UsesMatchingTierPresetCount()
        {
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 0, presetCount = 3, gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 1, weight = 1f } }, bossPresetIds = new List<string> { "unused" } },
                    new DifficultyTier { minCounter = 4, presetCount = 7, gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 1, weight = 1f } }, bossPresetIds = new List<string> { "unused" } },
                },
            };
            var presets = PresetsById(Preset("p1", 1, ArmyClass.None));

            var lowCounter = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1));
            var highCounter = EnemyCompositionGenerator.Generate(4, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1));

            Assert.AreEqual(3, lowCounter.Count);
            Assert.AreEqual(7, highCounter.Count);
        }

        [Test]
        public void Generate_CounterBelowFirstTier_ClampsToFirstTier()
        {
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 2, presetCount = 5, gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 1, weight = 1f } }, bossPresetIds = new List<string> { "unused" } },
                },
            };
            var presets = PresetsById(Preset("p1", 1, ArmyClass.None));

            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1));

            Assert.AreEqual(5, result.Count, "첫 구간의 minCounter보다 낮아도 첫 구간으로 클램프돼야 함");
        }

        [Test]
        public void Generate_SingleGradeWeight_OnlyPicksThatGrade()
        {
            var tier = new DifficultyTier
            {
                minCounter = 0, presetCount = 5,
                gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 1, weight = 1f } },
                bossPresetIds = new List<string> { "unused" },
            };
            var config = SingleTierConfig(tier);
            // grade 2 프리셋도 풀에 있지만 이 티어의 gradeWeights엔 없으므로 절대 뽑히면 안 됨.
            var presets = PresetsById(Preset("p_g1", 1, ArmyClass.None), Preset("p_g2", 2, ArmyClass.Warrior));

            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1));

            Assert.IsTrue(result.TrueForAll(e => e.armyClass == ArmyClass.None));
        }

        [Test]
        public void Generate_MultipleGradeWeights_CanProduceEveryGrade()
        {
            var tier = new DifficultyTier
            {
                minCounter = 0, presetCount = 200,
                gradeWeights = new List<GradeWeight>
                {
                    new GradeWeight { grade = 1, weight = 1f }, new GradeWeight { grade = 2, weight = 1f },
                    new GradeWeight { grade = 3, weight = 1f },
                },
                bossPresetIds = new List<string> { "unused" },
            };
            var config = SingleTierConfig(tier);
            var presets = PresetsById(
                Preset("p_g1", 1, ArmyClass.None), Preset("p_g2", 2, ArmyClass.Warrior), Preset("p_g3", 3, ArmyClass.Archer));

            var result = EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1));

            var producedClasses = result.Select(e => e.armyClass).Distinct().ToList();
            CollectionAssert.AreEquivalent(new[] { ArmyClass.None, ArmyClass.Warrior, ArmyClass.Archer }, producedClasses);
        }

        [Test]
        public void Generate_EmptyGradePool_Throws()
        {
            var tier = new DifficultyTier
            {
                minCounter = 0, presetCount = 1,
                gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 9, weight = 1f } }, // 이 등급 프리셋이 풀에 없음
                bossPresetIds = new List<string> { "unused" },
            };
            var config = SingleTierConfig(tier);
            var presets = PresetsById(Preset("p1", 1, ArmyClass.None));

            Assert.Throws<ArgumentException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1)));
        }

        [Test]
        public void Generate_NegativeCounter_Throws()
        {
            var config = new EnemyCompositionConfig();
            var presets = PresetsById(Preset("p1", 1, ArmyClass.None));
            Assert.Throws<ArgumentException>(() =>
                EnemyCompositionGenerator.Generate(-1, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, new Random(1)));
        }

        [Test]
        public void Generate_NullArguments_Throw()
        {
            var config = new EnemyCompositionConfig();
            var presets = PresetsById(Preset("p1", 1, ArmyClass.None));

            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, null, presets, ArmyDefs, NoAugmentData, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, null, ArmyDefs, NoAugmentData, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, null, NoAugmentData, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                EnemyCompositionGenerator.Generate(0, RoomType.NormalBattle, config, presets, ArmyDefs, NoAugmentData, null));
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
            var tier = new DifficultyTier { bossPresetIds = new List<string> { "x" } };
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 2, bossPresetIds = new List<string> { "x" } },
                    new DifficultyTier { minCounter = 2, bossPresetIds = new List<string> { "x" } },
                },
            };
            Assert.Throws<ArgumentException>(() => config.Validate(), "minCounter 중복은 허용하면 안 됨");
        }

        [Test]
        public void Validate_TierWithEmptyBossPresetIds_Throws()
        {
            var config = SingleTierConfig(new DifficultyTier { minCounter = 0, bossPresetIds = new List<string>() });
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_TierWithEmptyGradeWeights_Throws()
        {
            var config = SingleTierConfig(new DifficultyTier
            {
                minCounter = 0, gradeWeights = new List<GradeWeight>(), bossPresetIds = new List<string> { "x" },
            });
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void GetTierFor_ExactBoundary_UsesThatTier()
        {
            var config = new EnemyCompositionConfig
            {
                tiers = new List<DifficultyTier>
                {
                    new DifficultyTier { minCounter = 0, presetCount = 1, bossPresetIds = new List<string> { "x" } },
                    new DifficultyTier { minCounter = 3, presetCount = 2, bossPresetIds = new List<string> { "x" } },
                    new DifficultyTier { minCounter = 6, presetCount = 3, bossPresetIds = new List<string> { "x" } },
                },
            };

            Assert.AreEqual(1, config.GetTierFor(2).presetCount);
            Assert.AreEqual(2, config.GetTierFor(3).presetCount, "경계값(minCounter와 정확히 같음)은 그 구간을 써야 함");
            Assert.AreEqual(2, config.GetTierFor(5).presetCount);
            Assert.AreEqual(3, config.GetTierFor(100).presetCount, "마지막 구간을 넘어가면 마지막 구간 유지");
        }

        [Test]
        public void Clone_ReturnsIndependentCopy()
        {
            var original = new EnemyCompositionConfig();
            EnemyCompositionConfig clone = original.Clone();
            clone.tiers[0].presetCount = 999;
            clone.tiers[0].bossPresetIds.Add("extra");
            clone.tiers[0].gradeWeights.Add(new GradeWeight { grade = 99, weight = 1f });

            Assert.AreNotEqual(999, original.tiers[0].presetCount, "clone 변형이 원본에 영향을 주면 안 됨");
            Assert.AreNotEqual(original.tiers[0].bossPresetIds.Count, clone.tiers[0].bossPresetIds.Count,
                "리스트도 독립 복사돼야 함");
            Assert.AreNotEqual(original.tiers[0].gradeWeights.Count, clone.tiers[0].gradeWeights.Count);
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
