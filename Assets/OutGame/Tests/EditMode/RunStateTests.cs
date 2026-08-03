using System;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
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
            var config = new RunConfig { startingArmyCount = 3, startingArmyClass = ArmyClass.Warrior };
            RunState run = RunStateFactory.Create(NewMap(), config);

            Assert.AreEqual(3, run.armies.Count);
            Assert.AreEqual(3, run.armies.Select(a => a.instanceId).Distinct().Count(), "instanceId는 유일해야 함");
            Assert.IsTrue(run.armies.All(a => a.armyClass == ArmyClass.Warrior));
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
            original.selectedCharacterId = "char_1"; // §5.2.5
            original.ownedItemIds.Add("item_bow");
            original.armies[0].Bind("item_shield", ArmyClass.Warrior);
            original.armies[1].AddBonusSoldiers(6);
            original.visitedEventIds.Add("evt_recruit_deserters");
            original.visitedEventIds.Add("evt_old_armory");
            original.selectedAugmentIds.Add("aug_common_attack");
            original.selectedAugmentIds.Add("aug_common_attack"); // 중복 선택/스택 허용(§4-27) — 리스트 그대로 보존돼야 함
            original.deployment.Add(new ArmySlotAssignment { armyInstanceId = original.armies[0].instanceId, slotId = 4 });
            original.deployment.Add(new ArmySlotAssignment { armyInstanceId = original.armies[1].instanceId, slotId = 0 });
            MapProgress.Visit(original.mapState, MapProgress.GetSelectableNodes(original.mapState)[0].point);

            RunState restored = RunState.FromJson(original.ToJson());

            Assert.AreEqual(original.gold, restored.gold);
            Assert.AreEqual(original.selectedCharacterId, restored.selectedCharacterId);
            Assert.AreEqual(original.ownedItemIds, restored.ownedItemIds);
            Assert.AreEqual(original.visitedEventIds, restored.visitedEventIds);
            Assert.AreEqual(original.selectedAugmentIds, restored.selectedAugmentIds);
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

        [Test]
        public void FromJson_MissingSelectedCharacterId_Throws()
        {
            // 코드 리뷰 CRITICAL 수정 — 캐릭터 선택 화면(§5.2.5) 추가 이전에 저장됐거나 손상된 런은
            // selectedCharacterId가 비어 있을 수 있다. 이걸 여기서 걸러내지 않으면 "이어하기"는
            // 성공한 것처럼 보이다가 첫 전투 시작 시점에야 DeploymentState.BuildSetup에서 뒤늦게
            // 예외가 났다 — RunSaveService.Load는 이미 FromJson의 ArgumentException을 "손상된 저장
            // 파일"로 처리하므로, 여기서 막으면 이어하기 시점에 바로 거부된다.
            RunState run = RunStateFactory.Create(NewMap(), new RunConfig());
            Assert.IsTrue(string.IsNullOrEmpty(run.selectedCharacterId), "선행 조건: 생성 직후엔 미선택 상태");

            Assert.Throws<ArgumentException>(() => RunState.FromJson(run.ToJson()));
        }
    }
}
