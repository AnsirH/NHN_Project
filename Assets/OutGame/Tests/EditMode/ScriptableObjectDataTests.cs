using NUnit.Framework;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;
using OutGame.Logic.Items;
using OutGame.ScriptableObjects;
using UnityEngine;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// SO 에셋 → 순수 로직 데이터(ToData) 변환 검증. 인스펙터에서 채운 값이
    /// OutGame.Logic 계층에 그대로 전달되는지 확인한다.
    /// </summary>
    public class ScriptableObjectDataTests
    {
        [Test]
        public void ArmyDefinition_ToData_MapsConfiguredFields()
        {
            var so = ScriptableObject.CreateInstance<ArmyDefinition>();
            try
            {
                SetField(so, "armyId", "army_test");
                SetField(so, "displayName", "Test army");
                SetField(so, "baseSoldierCount", 17);
                SetField(so, "maxSoldierCount", 43);
                SetField(so, "generalName", "Test general");
                SetField(so, "generalPower", 13f);
                SetField(so, "generalHealth", 123f);
                SetField(so, "generalAttack", 19f);
                SetField(so, "generalDefense", 7f);
                SetField(so, "generalCritRate", 23f);
                SetField(so, "generalMoveSpeed", 81f);
                SetField(so, "description", "Test description");
                SetField(so, "soldierHealth", 67f);
                SetField(so, "soldierAttack", 11f);
                SetField(so, "soldierDefense", 3f);
                var data = so.ToData();
                Assert.AreEqual("army_test", data.id);
                Assert.AreEqual("Test army", data.displayName);
                Assert.AreEqual(17, data.baseSoldierCount);
                Assert.AreEqual(43, data.maxSoldierCount);
                Assert.AreEqual("Test general", data.generalName);
                Assert.AreEqual(13f, data.generalPower);
                Assert.AreEqual(123f, data.generalHealth);
                Assert.AreEqual(19f, data.generalAttack);
                Assert.AreEqual(7f, data.generalDefense);
                Assert.AreEqual(23f, data.generalCritRate);
                Assert.AreEqual(81f, data.generalMoveSpeed);
                Assert.AreEqual("Test description", data.description);
                Assert.AreEqual(67f, data.soldierHealth);
                Assert.AreEqual(11f, data.soldierAttack);
                Assert.AreEqual(3f, data.soldierDefense);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ArmyDefinition_ToData_WithCritRateAboveHundred_Throws()
        {
            // OnValidate()는 인스펙터 변경 시에만 발동하므로 리플렉션으로 직접 대입해 우회 —
            // ToData()가 ArmyData.Validate()를 실제로 호출하는지(코드 리뷰 HIGH 수정) 검증한다.
            var so = ScriptableObject.CreateInstance<ArmyDefinition>();
            try
            {
                var field = typeof(ArmyDefinition).GetField("generalCritRate",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field.SetValue(so, 150f);

                Assert.Throws<System.InvalidOperationException>(() => so.ToData());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void BattleFieldConfig_ToData_MapsCustomDimensions()
        {
            var so = ScriptableObject.CreateInstance<BattleFieldConfig>();
            try
            {
                SetField(so, "rows", 2);
                SetField(so, "columns", 5);
                var data = so.ToData();
                Assert.AreEqual(2, data.rows);
                Assert.AreEqual(5, data.columns);
                Assert.AreEqual(10, data.GenerateSlots().Count);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void BattlePowerConfigAsset_ToData_MapsCustomWeights()
        {
            var so = ScriptableObject.CreateInstance<BattlePowerConfigAsset>();
            try
            {
                SetField(so, "baseWeight", 2f);
                SetField(so, "archerWeight", 3f);
                SetField(so, "warriorWeight", 4f);
                SetField(so, "hunterWeight", 5f);
                SetField(so, "assassinWeight", 6f);
                var data = so.ToData();
                Assert.AreEqual(2f, data.WeightOf(ArmyClass.None));
                Assert.AreEqual(3f, data.WeightOf(ArmyClass.Archer));
                Assert.AreEqual(4f, data.WeightOf(ArmyClass.Warrior));
                Assert.AreEqual(5f, data.WeightOf(ArmyClass.Hunter));
                Assert.AreEqual(6f, data.WeightOf(ArmyClass.Assassin));
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void RunConfigAsset_ToData_MapsConfiguredValuesAndCopiesCosts()
        {
            var so = ScriptableObject.CreateInstance<RunConfigAsset>();
            try
            {
                SetField(so, "startingArmyClass", ArmyClass.Hunter);
                SetField(so, "startingArmyCount", 2);
                SetField(so, "startingGold", 71);
                SetField(so, "battleVictoryGold", 37);
                SetField(so, "maxArmyCount", 12);
                var costs = new[] { 11, 22, 33, 44, 55 };
                SetField(so, "armyUpgradeCosts", costs);
                var data = so.ToData();
                Assert.AreEqual(ArmyClass.Hunter, data.startingArmyClass);
                Assert.AreEqual(2, data.startingArmyCount);
                Assert.AreEqual(71, data.startingGold);
                Assert.AreEqual(37, data.battleVictoryGold);
                Assert.AreEqual(12, data.maxArmyCount);
                CollectionAssert.AreEqual(costs, data.armyUpgradeCosts);
                data.armyUpgradeCosts[0] = 999;
                Assert.AreEqual(11, so.ToData().armyUpgradeCosts[0]);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void RunConfigAsset_ToData_MaxArmyCountBelowStartingArmyCount_Throws()
        {
            var runConfigSo = ScriptableObject.CreateInstance<RunConfigAsset>();
            try
            {
                SetField(runConfigSo, "startingArmyCount", 5);
                SetField(runConfigSo, "maxArmyCount", 3);

                Assert.Throws<System.InvalidOperationException>(() => runConfigSo.ToData());
            }
            finally
            {
                Object.DestroyImmediate(runConfigSo);
            }
        }

        private static void SetField(ScriptableObject target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(target, value);
        }

        [Test]
        public void EnemyCompositionConfigAsset_ToConfig_MapsConfiguredTier()
        {
            var so = ScriptableObject.CreateInstance<EnemyCompositionConfigAsset>();
            try
            {
                SetField(so, "config", new EnemyCompositionConfig
                {
                    tiers = new System.Collections.Generic.List<DifficultyTier>
                    {
                        new DifficultyTier { minCounter = 0, presetCount = 7,
                            gradeWeights = new System.Collections.Generic.List<GradeWeight>
                            { new GradeWeight { grade = 3, weight = 2.5f } },
                            bossPresetIds = new System.Collections.Generic.List<string> { "boss_test" } },
                    },
                });
                var data = so.ToConfig();
                Assert.AreEqual(1, data.tiers.Count);
                Assert.AreEqual(0, data.tiers[0].minCounter);
                Assert.AreEqual(7, data.tiers[0].presetCount);
                Assert.AreEqual(3, data.tiers[0].gradeWeights.Single().grade);
                Assert.AreEqual(2.5f, data.tiers[0].gradeWeights.Single().weight);
                CollectionAssert.AreEqual(new[] { "boss_test" }, data.tiers[0].bossPresetIds);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void EnemyCompositionConfigAsset_ToConfig_ReturnsIndependentCopy()
        {
            var so = ScriptableObject.CreateInstance<EnemyCompositionConfigAsset>();
            try
            {
                EnemyCompositionConfig data = so.ToConfig();
                data.tiers[0].presetCount = 999;

                Assert.AreNotEqual(999, so.ToConfig().tiers[0].presetCount, "반환값 변형이 에셋에 영향을 주면 안 됨");
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void EnemyCompositionConfigAsset_ToConfig_WithInvalidValue_Throws()
        {
            var so = ScriptableObject.CreateInstance<EnemyCompositionConfigAsset>();
            try
            {
                var field = typeof(EnemyCompositionConfigAsset).GetField("config",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field.SetValue(so, new EnemyCompositionConfig
                {
                    tiers = new System.Collections.Generic.List<DifficultyTier>(), // 빈 tiers는 Validate 위반
                });

                Assert.Throws<System.InvalidOperationException>(() => so.ToConfig());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ItemDropConfigAsset_ToConfig_MapsAllConfiguredChances()
        {
            var so = ScriptableObject.CreateInstance<ItemDropConfigAsset>();
            try
            {
                SetField(so, "config", new ItemDropConfig
                {
                    archerDropChance = 0.1f, warriorDropChance = 0.3f,
                    hunterDropChance = 0.6f, assassinDropChance = 0.9f,
                });
                var data = so.ToConfig();
                Assert.AreEqual(0.1f, data.archerDropChance);
                Assert.AreEqual(0.3f, data.warriorDropChance);
                Assert.AreEqual(0.6f, data.hunterDropChance);
                Assert.AreEqual(0.9f, data.assassinDropChance);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ItemDropConfigAsset_ToConfig_WithInvalidValue_Throws()
        {
            var so = ScriptableObject.CreateInstance<ItemDropConfigAsset>();
            try
            {
                var field = typeof(ItemDropConfigAsset).GetField("config",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field.SetValue(so, new ItemDropConfig { archerDropChance = 1.5f });

                Assert.Throws<System.InvalidOperationException>(() => so.ToConfig());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }
    }
}
