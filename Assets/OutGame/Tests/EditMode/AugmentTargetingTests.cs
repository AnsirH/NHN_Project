using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// AugmentTargeting — ArmyStatCalculator(스탯 배율)와 DeploymentState(스킬 강화 횟수 집계)가
    /// 공유하는 "이 증강이 이 병과에 적용되는가" 판정 검증.
    /// </summary>
    public class AugmentTargetingTests
    {
        private static AugmentData StatAugment(ArmyClass targetClass = ArmyClass.None) => new AugmentData
        {
            id = "a", category = AugmentCategory.StatAugment, effectType = AugmentEffectType.StatBoost,
            targetStat = AugmentStat.Attack, statBoostPercent = 0.1f, targetArmyClass = targetClass,
        };

        private static AugmentData ItemAugment(ArmyClass targetClass, AugmentEffectType effectType = AugmentEffectType.StatBoost) => new AugmentData
        {
            id = "a", category = AugmentCategory.ItemAugment, effectType = effectType,
            targetArmyClass = targetClass, targetStat = AugmentStat.Attack, statBoostPercent = 0.1f,
        };

        [Test]
        public void AppliesToClass_StatAugment_AppliesRegardlessOfClass()
        {
            AugmentData augment = StatAugment();
            Assert.IsTrue(AugmentTargeting.AppliesToClass(augment, ArmyClass.Archer));
            Assert.IsTrue(AugmentTargeting.AppliesToClass(augment, ArmyClass.Warrior));
            Assert.IsTrue(AugmentTargeting.AppliesToClass(augment, ArmyClass.None));
        }

        [Test]
        public void AppliesToClass_ItemAugment_OnlyMatchingClass()
        {
            AugmentData augment = ItemAugment(ArmyClass.Archer);
            Assert.IsTrue(AugmentTargeting.AppliesToClass(augment, ArmyClass.Archer));
            Assert.IsFalse(AugmentTargeting.AppliesToClass(augment, ArmyClass.Warrior));
        }

        [Test]
        public void AppliesToClass_NullAugment_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AugmentTargeting.AppliesToClass(null, ArmyClass.Archer));
        }

        [Test]
        public void CountMatching_CountsOnlyMatchingClassAndEffectType()
        {
            var selected = new List<AugmentData>
            {
                ItemAugment(ArmyClass.Hunter, AugmentEffectType.GeneralSkillUpgrade), // 매칭
                ItemAugment(ArmyClass.Hunter, AugmentEffectType.GeneralSkillUpgrade), // 매칭 (스택)
                ItemAugment(ArmyClass.Assassin, AugmentEffectType.GeneralSkillUpgrade), // 다른 병과
                ItemAugment(ArmyClass.Hunter, AugmentEffectType.StatBoost), // 다른 effectType
            };

            int count = AugmentTargeting.CountMatching(ArmyClass.Hunter, AugmentEffectType.GeneralSkillUpgrade, selected);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void CountMatching_NoMatches_ReturnsZero()
        {
            var selected = new List<AugmentData> { StatAugment() };
            Assert.AreEqual(0, AugmentTargeting.CountMatching(ArmyClass.Hunter, AugmentEffectType.GeneralSkillUpgrade, selected));
        }

        [Test]
        public void CountMatching_NullSelectedAugments_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => AugmentTargeting.CountMatching(ArmyClass.Hunter, AugmentEffectType.GeneralSkillUpgrade, null));
        }
    }
}
