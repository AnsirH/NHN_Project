using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>전투 승리 보상 적용 검증 (§4-20/§9).</summary>
    public class BattleRewardApplierTests
    {
        private static RunState NewRun()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 9).Generate();
            return RunStateFactory.Create(map, new RunConfig { startingGold = 10 });
        }

        [Test]
        public void ApplyVictoryReward_AddsGoldToRun()
        {
            RunState run = NewRun();
            BattleRewardApplier.ApplyVictoryReward(run, 20);
            Assert.AreEqual(30, run.gold);
        }

        [Test]
        public void ApplyVictoryReward_ZeroGold_DoesNotThrow()
        {
            RunState run = NewRun();
            Assert.DoesNotThrow(() => BattleRewardApplier.ApplyVictoryReward(run, 0));
            Assert.AreEqual(10, run.gold);
        }

        [Test]
        public void ApplyVictoryReward_NullRun_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BattleRewardApplier.ApplyVictoryReward(null, 10));
        }

        [Test]
        public void ApplyVictoryReward_NegativeGold_Throws()
        {
            Assert.Throws<ArgumentException>(() => BattleRewardApplier.ApplyVictoryReward(NewRun(), -1));
        }

        [Test]
        public void ApplyItemDrops_AddsAllDroppedItemsToInventory()
        {
            RunState run = NewRun();
            BattleRewardApplier.ApplyItemDrops(run, new List<string> { "item_bow", "item_shield" });

            CollectionAssert.Contains(run.ownedItemIds, "item_bow");
            CollectionAssert.Contains(run.ownedItemIds, "item_shield");
        }

        [Test]
        public void ApplyItemDrops_EmptyList_DoesNotThrow()
        {
            RunState run = NewRun();
            Assert.DoesNotThrow(() => BattleRewardApplier.ApplyItemDrops(run, new List<string>()));
            Assert.IsEmpty(run.ownedItemIds);
        }

        [Test]
        public void ApplyItemDrops_DuplicateItemId_AllowsMultipleCopies()
        {
            // 부대 단위 부여이므로 같은 아이템을 여러 번 보유할 수 있다 (EventRewardApplier와 동일 근거).
            RunState run = NewRun();
            BattleRewardApplier.ApplyItemDrops(run, new List<string> { "item_bow", "item_bow" });

            Assert.AreEqual(2, run.ownedItemIds.FindAll(id => id == "item_bow").Count);
        }

        [Test]
        public void ApplyItemDrops_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => BattleRewardApplier.ApplyItemDrops(null, new List<string>()));
            Assert.Throws<ArgumentNullException>(() => BattleRewardApplier.ApplyItemDrops(NewRun(), null));
        }
    }
}
