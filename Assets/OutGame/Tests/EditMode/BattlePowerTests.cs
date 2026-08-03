using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Battle;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 전투력 계산 검증 (§4-22): Σ(병사 수 × 병과 계수 × 유닛 배율) + 장군 보정 × 장군 배율.
    /// </summary>
    public class BattlePowerTests
    {
        private static readonly Dictionary<string, ArmyData> Defs = new Dictionary<string, ArmyData>
        {
            ["army_basic"] = new ArmyData { id = "army_basic", baseSoldierCount = 30, generalPower = 10f },
        };

        private static readonly List<AugmentData> NoAugments = new List<AugmentData>();

        private static DeployedArmy Deployed(ArmyClass cls, int soldiers, int upgradeLevel = 0) => new DeployedArmy
        {
            armyInstanceId = System.Guid.NewGuid().ToString(),
            armyDefId = "army_basic",
            armyClass = cls,
            soldierCount = soldiers,
            upgradeLevel = upgradeLevel,
        };

        [Test]
        public void Calculate_EmptyDeployment_ReturnsZero()
        {
            float power = BattlePowerCalculator.Calculate(
                new List<DeployedArmy>(), new BattlePowerConfig(), Defs, NoAugments);
            Assert.AreEqual(0f, power);
        }

        [Test]
        public void Calculate_BasicArmy_UsesBaseWeightPlusGeneral()
        {
            // 30 × 1.0 + 10 = 40
            float power = BattlePowerCalculator.Calculate(
                new List<DeployedArmy> { Deployed(ArmyClass.None, 30) }, new BattlePowerConfig(), Defs, NoAugments);
            Assert.AreEqual(40f, power, 1e-3f);
        }

        [Test]
        public void Calculate_ArcherAndWarrior_UseClassWeights()
        {
            // (30 × 1.2 + 10) + (30 × 1.5 + 10) = 46 + 55 = 101
            var armies = new List<DeployedArmy> { Deployed(ArmyClass.Archer, 30), Deployed(ArmyClass.Warrior, 30) };
            float power = BattlePowerCalculator.Calculate(armies, new BattlePowerConfig(), Defs, NoAugments);
            Assert.AreEqual(101f, power, 1e-3f);
        }

        [Test]
        public void Calculate_HunterAndAssassin_UseClassWeights()
        {
            // 2026-07-26 4병과 확장(§4-28): (30×1.6+10) + (30×1.4+10) = 58 + 52 = 110
            var armies = new List<DeployedArmy> { Deployed(ArmyClass.Hunter, 30), Deployed(ArmyClass.Assassin, 30) };
            float power = BattlePowerCalculator.Calculate(armies, new BattlePowerConfig(), Defs, NoAugments);
            Assert.AreEqual(110f, power, 1e-3f);
        }

        [Test]
        public void Calculate_CustomWeights_AreRespected()
        {
            var config = new BattlePowerConfig { baseWeight = 2f, archerWeight = 3f, warriorWeight = 4f };
            // 10 × 3 + 10 = 40
            float power = BattlePowerCalculator.Calculate(
                new List<DeployedArmy> { Deployed(ArmyClass.Archer, 10) }, config, Defs, NoAugments);
            Assert.AreEqual(40f, power, 1e-3f);
        }

        [Test]
        public void Calculate_UnknownArmyDef_Throws()
        {
            var rogue = Deployed(ArmyClass.None, 10);
            rogue.armyDefId = "army_ghost";
            Assert.Throws<System.ArgumentException>(() => BattlePowerCalculator.Calculate(
                new List<DeployedArmy> { rogue }, new BattlePowerConfig(), Defs, NoAugments));
        }

        [Test]
        public void Calculate_NullArguments_Throw()
        {
            var armies = new List<DeployedArmy>();
            var config = new BattlePowerConfig();

            Assert.Throws<System.ArgumentNullException>(() => BattlePowerCalculator.Calculate(null, config, Defs, NoAugments));
            Assert.Throws<System.ArgumentNullException>(() => BattlePowerCalculator.Calculate(armies, null, Defs, NoAugments));
            Assert.Throws<System.ArgumentNullException>(() => BattlePowerCalculator.Calculate(armies, config, null, NoAugments));
            Assert.Throws<System.ArgumentNullException>(() => BattlePowerCalculator.Calculate(armies, config, Defs, null));
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

            float minimalPower = BattlePowerCalculator.Calculate(new List<DeployedArmy> { minimal }, new BattlePowerConfig(), Defs, NoAugments);
            float fullPower = BattlePowerCalculator.Calculate(new List<DeployedArmy> { full }, new BattlePowerConfig(), Defs, NoAugments);

            Assert.AreEqual(fullPower, minimalPower, 1e-3f);
        }

        [Test]
        public void Calculate_HigherUpgradeLevel_IncreasesPower()
        {
            // 회귀 테스트(2026-07-26): 업그레이드를 해도 전투력이 안 오르던 버그 — upgradeLevel이
            // 장군 보정과 유닛 배율 둘 다에 반영돼야 한다.
            var armies0 = new List<DeployedArmy> { Deployed(ArmyClass.Archer, 30, upgradeLevel: 0) };
            var armies3 = new List<DeployedArmy> { Deployed(ArmyClass.Archer, 30, upgradeLevel: 3) };

            float power0 = BattlePowerCalculator.Calculate(armies0, new BattlePowerConfig(), Defs, NoAugments);
            float power3 = BattlePowerCalculator.Calculate(armies3, new BattlePowerConfig(), Defs, NoAugments);

            // (30×1.2×1.3 + 10×1.3) = 46.8 + 13 = 59.8
            Assert.AreEqual(59.8f, power3, 1e-3f);
            Assert.Greater(power3, power0, "업그레이드 레벨이 높을수록 전투력이 더 높아야 함");
        }

        [Test]
        public void Calculate_SelectedAugment_AffectsPower()
        {
            // 유닛 항목엔 공격력 증강이 반영돼야 한다(ArmyInfoPopup과 동일 원칙).
            var augments = new List<AugmentData>
            {
                new AugmentData { id = "a", category = AugmentCategory.StatAugment, effectType = AugmentEffectType.StatBoost,
                    targetStat = AugmentStat.Attack, statBoostPercent = 0.5f },
            };
            var armies = new List<DeployedArmy> { Deployed(ArmyClass.None, 30) };

            float powerWithout = BattlePowerCalculator.Calculate(armies, new BattlePowerConfig(), Defs, NoAugments);
            float powerWith = BattlePowerCalculator.Calculate(armies, new BattlePowerConfig(), Defs, augments);

            Assert.Greater(powerWith, powerWithout, "선택된 공격력 증강은 전투력을 높여야 함");
        }

        [Test]
        public void Calculate_SelectedAugment_AlsoAffectsGeneralPowerTerm()
        {
            // 2026-07-26 사용자 확정: 장군도 증강 혜택을 받아야 한다 — soldierCount=0으로 병사 항목을
            // 없애 장군 보정(generalPower) 항목만 남긴 뒤에도 증강이 반영되는지 확인.
            var augments = new List<AugmentData>
            {
                new AugmentData { id = "a", category = AugmentCategory.StatAugment, effectType = AugmentEffectType.StatBoost,
                    targetStat = AugmentStat.Attack, statBoostPercent = 0.5f },
            };
            var armies = new List<DeployedArmy> { Deployed(ArmyClass.None, 0) };

            float powerWithout = BattlePowerCalculator.Calculate(armies, new BattlePowerConfig(), Defs, NoAugments);
            float powerWith = BattlePowerCalculator.Calculate(armies, new BattlePowerConfig(), Defs, augments);

            Assert.AreEqual(10f, powerWithout, 1e-3f, "병사 0명이면 장군 보정(10)만 남아야 함");
            Assert.AreEqual(10f * (1f + 1.5f + 1f) / 3f, powerWith, 1e-3f, "장군 보정에도 증강 평균 배율이 적용돼야 함");
        }

        [Test]
        public void WeightOf_UndefinedClass_Throws()
        {
            // 4병과(Warrior/Hunter/Assassin/Archer) + None 외의 값은 정의된 적이 없다 — enum 캐스트로
            // 임의의 미정의 값을 만들어 default 분기가 조용히 넘어가지 않고 던지는지 확인한다.
            Assert.Throws<System.ArgumentException>(() => new BattlePowerConfig().WeightOf((ArmyClass)999));
        }
    }
}
