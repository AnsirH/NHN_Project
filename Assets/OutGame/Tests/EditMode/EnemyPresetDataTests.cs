using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;

namespace OutGame.Tests.EditMode
{
    /// <summary>프리셋 조각 데이터 검증 (2026-08-02, §4-28).</summary>
    public class EnemyPresetDataTests
    {
        private static EnemyPresetUnit ValidUnit() => new EnemyPresetUnit
        {
            armyClass = ArmyClass.Warrior, soldierCount = 30, upgradeLevel = 0,
        };

        private static EnemyPresetData ValidPreset() => new EnemyPresetData
        {
            presetId = "preset_test", displayName = "테스트", grade = 1,
            units = new List<EnemyPresetUnit> { ValidUnit() },
        };

        [Test]
        public void Validate_ValidPreset_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ValidPreset().Validate());
        }

        [Test]
        public void Validate_EmptyPresetId_Throws()
        {
            var preset = ValidPreset();
            preset.presetId = "";
            Assert.Throws<ArgumentException>(() => preset.Validate());
        }

        [Test]
        public void Validate_NoUnits_Throws()
        {
            var preset = ValidPreset();
            preset.units = new List<EnemyPresetUnit>();
            Assert.Throws<ArgumentException>(() => preset.Validate());
        }

        [Test]
        public void Validate_GradeBelowOne_Throws()
        {
            var preset = ValidPreset();
            preset.grade = 0;
            Assert.Throws<ArgumentException>(() => preset.Validate());
        }

        [Test]
        public void Validate_UnitWithZeroSoldierCount_Throws()
        {
            var preset = ValidPreset();
            preset.units[0].soldierCount = 0;
            Assert.Throws<ArgumentException>(() => preset.Validate());
        }

        [Test]
        public void Validate_UnitWithNegativeUpgradeLevel_Throws()
        {
            var preset = ValidPreset();
            preset.units[0].upgradeLevel = -1;
            Assert.Throws<ArgumentException>(() => preset.Validate());
        }

        [Test]
        public void Clone_ReturnsIndependentCopy()
        {
            EnemyPresetData original = ValidPreset();
            original.units[0].selectedAugmentIds.Add("aug_original");
            EnemyPresetData clone = original.Clone();
            clone.units[0].soldierCount = 999;
            clone.units[0].selectedAugmentIds.Add("aug_extra");
            clone.units.Add(ValidUnit());

            Assert.AreNotEqual(999, original.units[0].soldierCount, "유닛 필드 변형이 원본에 영향을 주면 안 됨");
            Assert.AreEqual(1, original.units[0].selectedAugmentIds.Count, "selectedAugmentIds도 독립 복사돼야 함");
            Assert.AreNotEqual(original.units.Count, clone.units.Count, "units 리스트도 독립 복사돼야 함");
        }
    }
}
