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
                Assert.AreEqual(100f, data.generalHealth);
                Assert.AreEqual(10f, data.generalAttack);
                Assert.AreEqual(5f, data.generalDefense);
                Assert.AreEqual(5f, data.generalCritRate);
                Assert.AreEqual(100f, data.generalMoveSpeed);
                Assert.AreEqual(50f, data.soldierHealth);
                Assert.AreEqual(5f, data.soldierAttack);
                Assert.AreEqual(2f, data.soldierDefense);
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
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
