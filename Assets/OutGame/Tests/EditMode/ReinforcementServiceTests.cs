using System;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Reinforcement;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>증원 방 효과 검증 (§5.5): 기본값 기준 flat +20%, 여러 번 적용 시 매번 base 기준.</summary>
    public class ReinforcementServiceTests
    {
        private static ArmyData Def(int baseSoldiers) =>
            new ArmyData { id = "army_basic", baseSoldierCount = baseSoldiers, maxSoldierCount = baseSoldiers * 2 };
        private static ArmyInstance NewArmy() => new ArmyInstance { instanceId = Guid.NewGuid().ToString() };

        [Test]
        public void Reinforce_DefaultPercent_Adds20PercentOfBase()
        {
            ArmyInstance army = NewArmy();
            int added = ReinforcementService.Reinforce(army, Def(30));

            Assert.AreEqual(6, added); // 30 * 0.2 = 6
            Assert.AreEqual(6, army.bonusSoldierCount);
        }

        [Test]
        public void Reinforce_AppliedTwice_AddsFlatAmountEachTime()
        {
            ArmyInstance army = NewArmy();
            ArmyData def = Def(30);

            ReinforcementService.Reinforce(army, def);
            ReinforcementService.Reinforce(army, def);

            Assert.AreEqual(12, army.bonusSoldierCount, "매번 base 기준 flat +20% — 복리 아님");
        }

        [Test]
        public void Reinforce_RoundsToNearestInteger()
        {
            ArmyInstance army = NewArmy();
            int added = ReinforcementService.Reinforce(army, Def(25)); // 25 * 0.2 = 5.0 (정확히 나눠떨어지는 경우)

            Assert.AreEqual(5, added);
        }

        [Test]
        public void Reinforce_CustomPercent_IsRespected()
        {
            ArmyInstance army = NewArmy();
            int added = ReinforcementService.Reinforce(army, Def(30), bonusPercent: 0.5f);

            Assert.AreEqual(15, added);
        }

        [Test]
        public void Reinforce_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => ReinforcementService.Reinforce(null, Def(30)));
            Assert.Throws<ArgumentNullException>(() => ReinforcementService.Reinforce(NewArmy(), null));
        }

        [Test]
        public void Reinforce_NegativePercent_Throws()
        {
            Assert.Throws<ArgumentException>(() => ReinforcementService.Reinforce(NewArmy(), Def(30), bonusPercent: -0.1f));
        }

        [Test]
        public void Reinforce_NearCap_AddsOnlyRemainingRoom()
        {
            // base 30, max 60(=base×2) — 이미 bonus 27이면 남은 여유는 3뿐 (§4-26 상한).
            ArmyInstance army = NewArmy();
            army.AddBonusSoldiers(27);

            int added = ReinforcementService.Reinforce(army, Def(30)); // 원래는 +6이지만 여유가 3뿐

            Assert.AreEqual(3, added);
            Assert.AreEqual(30, army.bonusSoldierCount);
        }

        [Test]
        public void Reinforce_AtCap_AddsZero()
        {
            ArmyInstance army = NewArmy();
            army.AddBonusSoldiers(30); // base 30 + bonus 30 = max 60, 이미 상한

            int added = ReinforcementService.Reinforce(army, Def(30));

            Assert.AreEqual(0, added);
            Assert.AreEqual(30, army.bonusSoldierCount, "상한 도달 시 더 늘지 않음");
        }
    }
}
