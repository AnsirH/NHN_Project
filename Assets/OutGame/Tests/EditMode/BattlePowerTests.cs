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
        public void Calculate_ArcherAndCavalry_UseClassWeights()
        {
            // (30 × 1.2 + 10) + (30 × 1.5 + 10) = 46 + 55 = 101
            var armies = new List<DeployedArmy> { Deployed(ArmyClass.Archer, 30), Deployed(ArmyClass.Cavalry, 30) };
            float power = BattlePowerCalculator.Calculate(armies, new BattlePowerConfig(), Defs);
            Assert.AreEqual(101f, power, 1e-3f);
        }

        [Test]
        public void Calculate_CustomWeights_AreRespected()
        {
            var config = new BattlePowerConfig { baseWeight = 2f, archerWeight = 3f, cavalryWeight = 4f };
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
        public void WeightOf_ReservedClass_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new BattlePowerConfig().WeightOf(ArmyClass.Spearman));
        }
    }
}
