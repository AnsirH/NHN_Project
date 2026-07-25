using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 전투력 계산 검증 (§4-22): Σ(병사 수 × 병과 계수) + 장군 보정.
    /// </summary>
    public class BattlePowerTests
    {
        private static readonly Dictionary<string, ArmyData> Defs = new Dictionary<string, ArmyData>
        {
            ["army_basic"] = new ArmyData { id = "army_basic", baseSoldierCount = 30, generalPower = 10f },
        };

        private static DeployedArmy Deployed(ArmyClass cls, int soldiers) => new DeployedArmy
        {
            armyInstanceId = System.Guid.NewGuid().ToString(),
            armyDefId = "army_basic",
            armyClass = cls,
            soldierCount = soldiers,
        };

        [Test]
        public void Calculate_EmptyDeployment_ReturnsZero()
        {
            float power = BattlePowerCalculator.Calculate(
                new List<DeployedArmy>(), new BattlePowerConfig(), Defs);
            Assert.AreEqual(0f, power);
        }

        [Test]
        public void Calculate_BasicArmy_UsesBaseWeightPlusGeneral()
        {
            // 30 × 1.0 + 10 = 40
            float power = BattlePowerCalculator.Calculate(
                new List<DeployedArmy> { Deployed(ArmyClass.None, 30) }, new BattlePowerConfig(), Defs);
            Assert.AreEqual(40f, power, 1e-3f);
        }

        [Test]
        public void Calculate_ArcherAndShieldman_UseClassWeights()
        {
            // (30 × 1.2 + 10) + (30 × 1.5 + 10) = 46 + 55 = 101
            var armies = new List<DeployedArmy> { Deployed(ArmyClass.Archer, 30), Deployed(ArmyClass.Shieldman, 30) };
            float power = BattlePowerCalculator.Calculate(armies, new BattlePowerConfig(), Defs);
            Assert.AreEqual(101f, power, 1e-3f);
        }

        [Test]
        public void Calculate_CustomWeights_AreRespected()
        {
            var config = new BattlePowerConfig { baseWeight = 2f, archerWeight = 3f, shieldmanWeight = 4f };
            // 10 × 3 + 10 = 40
            float power = BattlePowerCalculator.Calculate(
                new List<DeployedArmy> { Deployed(ArmyClass.Archer, 10) }, config, Defs);
            Assert.AreEqual(40f, power, 1e-3f);
        }

        [Test]
        public void Calculate_UnknownArmyDef_Throws()
        {
            var rogue = Deployed(ArmyClass.None, 10);
            rogue.armyDefId = "army_ghost";
            Assert.Throws<System.ArgumentException>(() => BattlePowerCalculator.Calculate(
                new List<DeployedArmy> { rogue }, new BattlePowerConfig(), Defs));
        }

        [Test]
        public void Calculate_NullArguments_Throw()
        {
            var armies = new List<DeployedArmy>();
            var config = new BattlePowerConfig();

            Assert.Throws<System.ArgumentNullException>(() => BattlePowerCalculator.Calculate(null, config, Defs));
            Assert.Throws<System.ArgumentNullException>(() => BattlePowerCalculator.Calculate(armies, null, Defs));
            Assert.Throws<System.ArgumentNullException>(() => BattlePowerCalculator.Calculate(armies, config, null));
        }

        [Test]
        public void Calculate_IgnoresFieldsUnrelatedToPower()
        {
            // ArmyDeploymentPanel이 적 진영을 위해 armyInstanceId/equippedItemId/generalSkillId/
            // slotId/slotX/slotY를 비워둔 채(§4-28, EnemyArmy는 이 개념이 없음) DeployedArmy를
            // 만들어 넘기는 걸 전제로 한다 — Calculate가 이 필드들을 실제로 안 읽는지 고정해둔다.
            // 이 테스트가 깨지면 그 전제가 깨진 것이므로 해당 호출부도 함께 점검해야 한다.
            var minimal = new DeployedArmy { armyDefId = "army_basic", armyClass = ArmyClass.Archer, soldierCount = 30 };
            var full = new DeployedArmy
            {
                armyInstanceId = "some_instance",
                armyDefId = "army_basic",
                armyClass = ArmyClass.Archer,
                equippedItemId = "item_bow",
                generalSkillId = "skill_volley",
                soldierCount = 30,
                slotId = 7,
                slotX = 0.5f,
                slotY = 0.5f,
            };

            float minimalPower = BattlePowerCalculator.Calculate(new List<DeployedArmy> { minimal }, new BattlePowerConfig(), Defs);
            float fullPower = BattlePowerCalculator.Calculate(new List<DeployedArmy> { full }, new BattlePowerConfig(), Defs);

            Assert.AreEqual(fullPower, minimalPower, 1e-3f);
        }

        [Test]
        public void WeightOf_ReservedClass_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new BattlePowerConfig().WeightOf(ArmyClass.Spearman));
            Assert.Throws<System.ArgumentException>(() => new BattlePowerConfig().WeightOf(ArmyClass.Cavalry),
                "Cavalry는 2026-07-19부로 미사용 예약으로 격하됨 (§4-25)");
        }
    }
}
