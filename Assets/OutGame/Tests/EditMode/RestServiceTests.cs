using System;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Rest;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>휴식 방 증원 검증 (§5.5): 기본값 기준 flat +20%, 여러 번 적용 시 매번 base 기준.</summary>
    public class RestServiceTests
    {
        private static ArmyData Def(int baseSoldiers) => new ArmyData { id = "army_basic", baseSoldierCount = baseSoldiers };
        private static ArmyInstance NewArmy() => new ArmyInstance { instanceId = Guid.NewGuid().ToString(), armyDefId = "army_basic" };

        [Test]
        public void Reinforce_DefaultPercent_Adds20PercentOfBase()
        {
            ArmyInstance army = NewArmy();
            int added = RestService.Reinforce(army, Def(30));

            Assert.AreEqual(6, added); // 30 * 0.2 = 6
            Assert.AreEqual(6, army.bonusSoldierCount);
        }

        [Test]
        public void Reinforce_AppliedTwice_AddsFlatAmountEachTime()
        {
            ArmyInstance army = NewArmy();
            ArmyData def = Def(30);

            RestService.Reinforce(army, def);
            RestService.Reinforce(army, def);

            Assert.AreEqual(12, army.bonusSoldierCount, "매번 base 기준 flat +20% — 복리 아님");
        }

        [Test]
        public void Reinforce_RoundsToNearestInteger()
        {
            ArmyInstance army = NewArmy();
            int added = RestService.Reinforce(army, Def(25)); // 25 * 0.2 = 5.0 (정확히 나눠떨어지는 경우)

            Assert.AreEqual(5, added);
        }

        [Test]
        public void Reinforce_CustomPercent_IsRespected()
        {
            ArmyInstance army = NewArmy();
            int added = RestService.Reinforce(army, Def(30), bonusPercent: 0.5f);

            Assert.AreEqual(15, added);
        }

        [Test]
        public void Reinforce_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => RestService.Reinforce(null, Def(30)));
            Assert.Throws<ArgumentNullException>(() => RestService.Reinforce(NewArmy(), null));
        }

        [Test]
        public void Reinforce_NegativePercent_Throws()
        {
            Assert.Throws<ArgumentException>(() => RestService.Reinforce(NewArmy(), Def(30), bonusPercent: -0.1f));
        }
    }
}
