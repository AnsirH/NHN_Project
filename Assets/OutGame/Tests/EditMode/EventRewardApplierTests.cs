using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Events;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>보상 적용 검증 (§5.4, §4-20): 군대/아이템/재화 지급.</summary>
    public class EventRewardApplierTests
    {
        private RunState run;

        [SetUp]
        public void SetUp()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), 1).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 1, startingGold = 0 });
        }

        [Test]
        public void Apply_ArmyReward_AddsNewArmyInstance()
        {
            EventRewardApplier.Apply(run, new List<RewardGrant>
            {
                new RewardGrant { type = RewardType.Army, armyDefId = "army_basic" },
            });

            Assert.AreEqual(2, run.armies.Count);
            Assert.IsTrue(run.armies.Select(a => a.instanceId).Distinct().Count() == 2, "instanceId 유일해야 함");
            Assert.AreEqual("army_basic", run.armies[1].armyDefId);
        }

        [Test]
        public void Apply_ItemReward_AddsToOwnedItems()
        {
            EventRewardApplier.Apply(run, new List<RewardGrant>
            {
                new RewardGrant { type = RewardType.Item, itemId = "item_bow" },
            });

            Assert.Contains("item_bow", run.ownedItemIds);
        }

        [Test]
        public void Apply_ItemReward_AllowsDuplicates()
        {
            var rewards = new List<RewardGrant>
            {
                new RewardGrant { type = RewardType.Item, itemId = "item_bow" },
                new RewardGrant { type = RewardType.Item, itemId = "item_bow" },
            };
            EventRewardApplier.Apply(run, rewards);

            Assert.AreEqual(2, run.ownedItemIds.Count(id => id == "item_bow"), "군대 단위로 부여하므로 중복 보유 가능해야 함");
        }

        [Test]
        public void Apply_GoldReward_AddsToBalance()
        {
            EventRewardApplier.Apply(run, new List<RewardGrant>
            {
                new RewardGrant { type = RewardType.Gold, goldAmount = 50 },
            });

            Assert.AreEqual(50, run.gold);
        }

        [Test]
        public void Apply_MultipleRewards_AllApplied()
        {
            EventRewardApplier.Apply(run, new List<RewardGrant>
            {
                new RewardGrant { type = RewardType.Gold, goldAmount = 30 },
                new RewardGrant { type = RewardType.Item, itemId = "item_saddle" },
            });

            Assert.AreEqual(30, run.gold);
            Assert.Contains("item_saddle", run.ownedItemIds);
        }

        [Test]
        public void Apply_NoneReward_IsNoOp()
        {
            EventRewardApplier.Apply(run, new List<RewardGrant> { new RewardGrant { type = RewardType.None } });

            Assert.AreEqual(1, run.armies.Count);
            Assert.IsEmpty(run.ownedItemIds);
            Assert.AreEqual(0, run.gold);
        }

        [Test]
        public void Apply_NullRunOrRewards_Throw()
        {
            Assert.Throws<System.ArgumentNullException>(() => EventRewardApplier.Apply(null, new List<RewardGrant>()));
            Assert.Throws<System.ArgumentNullException>(() => EventRewardApplier.Apply(run, null));
        }
    }
}
