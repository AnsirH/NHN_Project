using System;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 런 생성(§5.2: 기본 군대 지급)과 RunState 직렬화(§6: 이어하기) 검증.
    /// </summary>
    public class RunStateTests
    {
        private static MapState NewMap() => new MapGenerator(new MapGenerationConfig(), 42).Generate();

        [Test]
        public void Create_GivesStartingArmiesWithUniqueIds()
        {
            var config = new RunConfig { startingArmyCount = 3, startingArmyDefId = "army_basic" };
            RunState run = RunStateFactory.Create(NewMap(), config);

            Assert.AreEqual(3, run.armies.Count);
            Assert.AreEqual(3, run.armies.Select(a => a.instanceId).Distinct().Count(), "instanceId는 유일해야 함");
            Assert.IsTrue(run.armies.All(a => a.armyDefId == "army_basic"));
            Assert.IsTrue(run.armies.All(a => !a.HasItem), "시작 군대는 아이템 없음 (§4-13)");
            Assert.IsTrue(run.armies.All(a => a.bonusSoldierCount == 0));
        }

        [Test]
        public void Create_InitializesGoldAndEmptyInventory()
        {
            var config = new RunConfig { startingGold = 100 };
            RunState run = RunStateFactory.Create(NewMap(), config);

            Assert.AreEqual(100, run.gold);
            Assert.IsEmpty(run.ownedItemIds);
            Assert.IsNotNull(run.mapState);
            Assert.IsEmpty(run.mapState.visitedPath, "새 런은 방문 기록 없음");
        }

        [Test]
        public void Create_NullMap_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RunStateFactory.Create(null, new RunConfig()));
        }

        [Test]
        public void Create_InvalidConfig_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => RunStateFactory.Create(NewMap(), new RunConfig { startingArmyCount = 0 }));
        }

        [Test]
        public void ToJson_FromJson_RoundTrip_PreservesEverything()
        {
            RunState original = RunStateFactory.Create(NewMap(), new RunConfig { startingGold = 50 });
            original.ownedItemIds.Add("item_bow");
            original.armies[0].Bind("item_saddle");
            original.armies[1].AddBonusSoldiers(6);
            original.visitedEventIds.Add("evt_recruit_deserters");
            original.visitedEventIds.Add("evt_old_armory");
            original.deployment.Add(new ArmySlotAssignment { armyInstanceId = original.armies[0].instanceId, slotId = 4 });
            original.deployment.Add(new ArmySlotAssignment { armyInstanceId = original.armies[1].instanceId, slotId = 0 });
            MapProgress.Visit(original.mapState, MapProgress.GetSelectableNodes(original.mapState)[0].point);

            RunState restored = RunState.FromJson(original.ToJson());

            Assert.AreEqual(original.gold, restored.gold);
            Assert.AreEqual(original.ownedItemIds, restored.ownedItemIds);
            Assert.AreEqual(original.visitedEventIds, restored.visitedEventIds);
            Assert.AreEqual(original.deployment.Count, restored.deployment.Count);
            for (int i = 0; i < original.deployment.Count; i++)
            {
                Assert.AreEqual(original.deployment[i].armyInstanceId, restored.deployment[i].armyInstanceId);
                Assert.AreEqual(original.deployment[i].slotId, restored.deployment[i].slotId);
            }
            Assert.AreEqual(original.armies.Count, restored.armies.Count);
            for (int i = 0; i < original.armies.Count; i++)
            {
                Assert.AreEqual(original.armies[i].instanceId, restored.armies[i].instanceId);
                // JsonUtility는 null 문자열을 ""로 직렬화한다 — HasItem 기준으로 동등성을 판단한다
                Assert.AreEqual(original.armies[i].HasItem, restored.armies[i].HasItem);
                if (original.armies[i].HasItem)
                    Assert.AreEqual(original.armies[i].EquippedItemId, restored.armies[i].EquippedItemId);
                Assert.AreEqual(original.armies[i].bonusSoldierCount, restored.armies[i].bonusSoldierCount);
            }
            Assert.AreEqual(original.mapState.visitedPath, restored.mapState.visitedPath);
            Assert.AreEqual(original.mapState.nodes.Count, restored.mapState.nodes.Count);
        }

        [Test]
        public void FromJson_Invalid_Throws()
        {
            Assert.Throws<ArgumentException>(() => RunState.FromJson(""));
            Assert.Throws<ArgumentException>(() => RunState.FromJson("{}"));
            Assert.Throws<ArgumentException>(() => RunState.FromJson("not json"));
        }
    }
}
