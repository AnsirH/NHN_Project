using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Items;

namespace OutGame.Tests.EditMode
{
    /// <summary>전투 승리 아이템 드롭 판정 검증 (§4-28) — 확률 0/1 극단값으로 결정적 테스트.</summary>
    public class ItemDropCalculatorTests
    {
        private static readonly Dictionary<ArmyClass, string> ItemIdByClass = new Dictionary<ArmyClass, string>
        {
            [ArmyClass.Archer] = "item_bow",
            [ArmyClass.Shieldman] = "item_shield",
        };

        [Test]
        public void RollDrops_GuaranteedChance_DropsForEveryClassedEnemy()
        {
            var config = new ItemDropConfig { archerDropChance = 1f, shieldmanDropChance = 1f };
            var composition = new List<ArmyClass> { ArmyClass.Archer, ArmyClass.Archer, ArmyClass.Shieldman };

            List<string> drops = ItemDropCalculator.RollDrops(composition, config, ItemIdByClass, new Random(1));

            Assert.AreEqual(3, drops.Count);
            Assert.AreEqual(2, drops.FindAll(id => id == "item_bow").Count);
            Assert.AreEqual(1, drops.FindAll(id => id == "item_shield").Count);
        }

        [Test]
        public void RollDrops_ZeroChance_NeverDrops()
        {
            var config = new ItemDropConfig { archerDropChance = 0f, shieldmanDropChance = 0f };
            var composition = new List<ArmyClass> { ArmyClass.Archer, ArmyClass.Shieldman };

            List<string> drops = ItemDropCalculator.RollDrops(composition, config, ItemIdByClass, new Random(1));

            Assert.IsEmpty(drops);
        }

        [Test]
        public void RollDrops_ClasslessEnemies_NeverRolled()
        {
            var config = new ItemDropConfig { archerDropChance = 1f, shieldmanDropChance = 1f };
            var composition = new List<ArmyClass> { ArmyClass.None, ArmyClass.None };

            List<string> drops = ItemDropCalculator.RollDrops(composition, config, ItemIdByClass, new Random(1));

            Assert.IsEmpty(drops, "병과 없는 적은 드롭 판정 자체를 하지 않아야 함");
        }

        [Test]
        public void RollDrops_ClassWithoutMappedItem_IsSkipped()
        {
            var config = new ItemDropConfig { archerDropChance = 1f, shieldmanDropChance = 1f };
            var composition = new List<ArmyClass> { ArmyClass.Archer };

            List<string> drops = ItemDropCalculator.RollDrops(
                composition, config, new Dictionary<ArmyClass, string>(), new Random(1));

            Assert.IsEmpty(drops, "매핑된 아이템이 없는 병과는 드롭하지 않아야 함");
        }

        [Test]
        public void RollDrops_NullArguments_Throw()
        {
            var config = new ItemDropConfig();
            Assert.Throws<ArgumentNullException>(() =>
                ItemDropCalculator.RollDrops(null, config, ItemIdByClass, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                ItemDropCalculator.RollDrops(new List<ArmyClass>(), null, ItemIdByClass, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                ItemDropCalculator.RollDrops(new List<ArmyClass>(), config, null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() =>
                ItemDropCalculator.RollDrops(new List<ArmyClass>(), config, ItemIdByClass, null));
        }

        [Test]
        public void Validate_DefaultConfig_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new ItemDropConfig().Validate());
        }

        [Test]
        public void Validate_ChanceAboveOne_Throws()
        {
            var config = new ItemDropConfig { archerDropChance = 1.5f };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }
    }
}
