using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>군대 업그레이드+증강 합산 스탯 배율 검증 (§4-26, §4-27).</summary>
    public class ArmyStatCalculatorTests
    {
        private static ArmyInstance Army(int upgradeLevel = 0) =>
            new ArmyInstance { instanceId = Guid.NewGuid().ToString(), armyDefId = "army_basic", upgradeLevel = upgradeLevel };

        [Test]
        public void GetStatMultiplier_NoAugments_MatchesUpgradeMultiplierOnly()
        {
            float result = ArmyStatCalculator.GetStatMultiplier(
                Army(upgradeLevel: 2), ArmyClass.Archer, AugmentStat.Health, new List<AugmentData>());

            Assert.AreEqual(1.2f, result, 1e-3f);
        }

        [Test]
        public void GetStatMultiplier_StatAugment_AppliesRegardlessOfClass()
        {
            var augments = new List<AugmentData>
            {
                new AugmentData { id = "a", category = AugmentCategory.StatAugment, effectType = AugmentEffectType.StatBoost,
                    targetStat = AugmentStat.Attack, statBoostPercent = 0.1f },
            };

            float archerResult = ArmyStatCalculator.GetStatMultiplier(Army(), ArmyClass.Archer, AugmentStat.Attack, augments);
            float shieldmanResult = ArmyStatCalculator.GetStatMultiplier(Army(), ArmyClass.Shieldman, AugmentStat.Attack, augments);

            Assert.AreEqual(1.1f, archerResult, 1e-3f);
            Assert.AreEqual(1.1f, shieldmanResult, 1e-3f, "공통 스탯 증강은 병과 무관 전군 적용");
        }

        [Test]
        public void GetStatMultiplier_ItemAugment_OnlyAppliesToMatchingClass()
        {
            var augments = new List<AugmentData>
            {
                new AugmentData { id = "a", category = AugmentCategory.ItemAugment, targetArmyClass = ArmyClass.Archer,
                    effectType = AugmentEffectType.StatBoost, targetStat = AugmentStat.Attack, statBoostPercent = 0.15f },
            };

            float archerResult = ArmyStatCalculator.GetStatMultiplier(Army(), ArmyClass.Archer, AugmentStat.Attack, augments);
            float shieldmanResult = ArmyStatCalculator.GetStatMultiplier(Army(), ArmyClass.Shieldman, AugmentStat.Attack, augments);

            Assert.AreEqual(1.15f, archerResult, 1e-3f);
            Assert.AreEqual(1.0f, shieldmanResult, 1e-3f, "다른 병과에는 아이템 증강이 적용되면 안 됨");
        }

        [Test]
        public void GetStatMultiplier_WrongStat_DoesNotApply()
        {
            var augments = new List<AugmentData>
            {
                new AugmentData { id = "a", category = AugmentCategory.StatAugment, effectType = AugmentEffectType.StatBoost,
                    targetStat = AugmentStat.Attack, statBoostPercent = 0.5f },
            };

            float result = ArmyStatCalculator.GetStatMultiplier(Army(), ArmyClass.Archer, AugmentStat.Health, augments);

            Assert.AreEqual(1.0f, result, 1e-3f, "다른 스탯을 대상으로 한 증강은 영향 없어야 함");
        }

        [Test]
        public void GetStatMultiplier_GeneralSkillUpgrade_HasNoNumericEffect()
        {
            var augments = new List<AugmentData>
            {
                new AugmentData { id = "a", category = AugmentCategory.ItemAugment, targetArmyClass = ArmyClass.Archer,
                    effectType = AugmentEffectType.GeneralSkillUpgrade },
            };

            float result = ArmyStatCalculator.GetStatMultiplier(Army(), ArmyClass.Archer, AugmentStat.Attack, augments);

            Assert.AreEqual(1.0f, result, 1e-3f, "장군 스킬 강화는 수치 효과가 없음(인게임 책임, §4-23)");
        }

        [Test]
        public void GetStatMultiplier_StacksMultipleAugmentsAdditively()
        {
            var augments = new List<AugmentData>
            {
                new AugmentData { id = "a", category = AugmentCategory.StatAugment, effectType = AugmentEffectType.StatBoost,
                    targetStat = AugmentStat.Defense, statBoostPercent = 0.1f },
                new AugmentData { id = "b", category = AugmentCategory.StatAugment, effectType = AugmentEffectType.StatBoost,
                    targetStat = AugmentStat.Defense, statBoostPercent = 0.1f },
            };

            float result = ArmyStatCalculator.GetStatMultiplier(Army(upgradeLevel: 1), ArmyClass.None, AugmentStat.Defense, augments);

            Assert.AreEqual(1.3f, result, 1e-3f, "업그레이드(+10%) + 증강 두 개(+10%+10%) = +30%");
        }

        [Test]
        public void GetStatMultiplier_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ArmyStatCalculator.GetStatMultiplier(null, ArmyClass.Archer, AugmentStat.Health, new List<AugmentData>()));
            Assert.Throws<ArgumentNullException>(() =>
                ArmyStatCalculator.GetStatMultiplier(Army(), ArmyClass.Archer, AugmentStat.Health, null));
        }
    }
}
