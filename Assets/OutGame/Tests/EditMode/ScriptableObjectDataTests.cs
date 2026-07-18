using NUnit.Framework;
using OutGame.Logic.Armies;
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
        public void ArmyDefinition_ToData_MapsAllFields()
        {
            var so = ScriptableObject.CreateInstance<ArmyDefinition>();
            try
            {
                var data = so.ToData();
                Assert.AreEqual("army_basic", data.id);
                Assert.AreEqual(30, data.baseSoldierCount);
                Assert.AreEqual(10f, data.generalPower);
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void BattleFieldConfig_ToData_Defaults3x3()
        {
            var so = ScriptableObject.CreateInstance<BattleFieldConfig>();
            try
            {
                var data = so.ToData();
                Assert.AreEqual(3, data.rows);
                Assert.AreEqual(3, data.columns);
                Assert.AreEqual(9, data.GenerateSlots().Count);
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void BattlePowerConfigAsset_ToData_DefaultsMatchSpec()
        {
            var so = ScriptableObject.CreateInstance<BattlePowerConfigAsset>();
            try
            {
                var data = so.ToData();
                Assert.AreEqual(1.0f, data.WeightOf(ArmyClass.None));
                Assert.AreEqual(1.2f, data.WeightOf(ArmyClass.Archer));
                Assert.AreEqual(1.5f, data.WeightOf(ArmyClass.Cavalry));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void RunConfigAsset_ToData_WithoutStartingArmy_Throws()
        {
            var so = ScriptableObject.CreateInstance<RunConfigAsset>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => so.ToData());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void RunConfigAsset_ToData_MapsBattleVictoryGoldDefault()
        {
            var armySo = ScriptableObject.CreateInstance<ArmyDefinition>();
            var runConfigSo = ScriptableObject.CreateInstance<RunConfigAsset>();
            try
            {
                var field = typeof(RunConfigAsset).GetField("startingArmy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field.SetValue(runConfigSo, armySo);

                var data = runConfigSo.ToData();
                Assert.AreEqual(20, data.battleVictoryGold, "§9 초안값 — 밸런스 튜닝 전까지 20");
                Assert.AreEqual(28, data.maxArmyCount, "§4-7 — BattleFieldConfig 기본 4×7과 일치하는 기본값");
            }
            finally
            {
                Object.DestroyImmediate(armySo);
                Object.DestroyImmediate(runConfigSo);
            }
        }

        [Test]
        public void RunConfigAsset_ToData_MaxArmyCountBelowStartingArmyCount_Throws()
        {
            var armySo = ScriptableObject.CreateInstance<ArmyDefinition>();
            var runConfigSo = ScriptableObject.CreateInstance<RunConfigAsset>();
            try
            {
                SetField(runConfigSo, "startingArmy", armySo);
                SetField(runConfigSo, "startingArmyCount", 5);
                SetField(runConfigSo, "maxArmyCount", 3);

                Assert.Throws<System.InvalidOperationException>(() => runConfigSo.ToData());
            }
            finally
            {
                Object.DestroyImmediate(armySo);
                Object.DestroyImmediate(runConfigSo);
            }
        }

        private static void SetField(RunConfigAsset target, string fieldName, object value)
        {
            var field = typeof(RunConfigAsset).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(target, value);
        }
    }
}
