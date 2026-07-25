using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 배치 슬롯 생성(§5.7)과 배치 편집 규칙, BattleSetupData 생성(§7.1) 검증.
    /// </summary>
    public class DeploymentTests
    {
        private static readonly Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>
        {
            ["item_bow"] = new ItemData { id = "item_bow", armyClass = ArmyClass.Archer, generalSkillId = "skill_volley" },
        };

        private static readonly Dictionary<string, ArmyData> Defs = new Dictionary<string, ArmyData>
        {
            ["army_basic"] = new ArmyData { id = "army_basic", baseSoldierCount = 30, generalPower = 10f },
        };

        private RunState run;
        private DeploymentState deployment;

        [SetUp]
        public void SetUp()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), 42).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 3 });
            deployment = new DeploymentState(new BattleFieldConfigData().GenerateSlots());
        }

        // ── 슬롯 생성 ────────────────────────────────────────────────

        [Test]
        public void GenerateSlots_Default3x3_Produces9UniqueSlotsInUnitRange()
        {
            List<SlotDefinition> slots = new BattleFieldConfigData().GenerateSlots();

            Assert.AreEqual(9, slots.Count);
            Assert.AreEqual(9, slots.Select(s => s.slotId).Distinct().Count());
            Assert.IsTrue(slots.All(s => s.x > 0f && s.x < 1f && s.y > 0f && s.y < 1f),
                "정규화 좌표는 (0,1) 개구간 안이어야 함 (§5.7)");
        }

        [Test]
        public void GenerateSlots_CustomGrid_RespectsRowsAndColumns()
        {
            var config = new BattleFieldConfigData { rows = 2, columns = 4 };
            Assert.AreEqual(8, config.GenerateSlots().Count);
        }

        [Test]
        public void GenerateSlots_InvalidGrid_Throws()
        {
            Assert.Throws<ArgumentException>(() => new BattleFieldConfigData { rows = 0 }.GenerateSlots());
            Assert.Throws<ArgumentException>(() => new BattleFieldConfigData { columns = 0 }.GenerateSlots());
        }

        [Test]
        public void GenerateSlots_1x1_ProducesSingleCenteredSlot()
        {
            List<SlotDefinition> slots = new BattleFieldConfigData { rows = 1, columns = 1 }.GenerateSlots();

            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual(0.5f, slots[0].x, 1e-3f);
            Assert.AreEqual(0.5f, slots[0].y, 1e-3f);
        }

        // ── 배치 편집 ────────────────────────────────────────────────

        [Test]
        public void Place_IntoEmptySlot_Deploys()
        {
            string army = run.armies[0].instanceId;
            deployment.Place(army, 0);

            Assert.AreEqual(army, deployment.GetArmyAt(0));
            Assert.AreEqual(0, deployment.GetSlotOf(army));
            Assert.AreEqual(1, deployment.DeployedCount);
        }

        [Test]
        public void Place_SameArmyToAnotherSlot_Moves()
        {
            string army = run.armies[0].instanceId;
            deployment.Place(army, 0);
            deployment.Place(army, 4);

            Assert.IsNull(deployment.GetArmyAt(0));
            Assert.AreEqual(army, deployment.GetArmyAt(4));
            Assert.AreEqual(1, deployment.DeployedCount, "이동은 중복 배치가 아님");
        }

        [Test]
        public void Place_DeployedArmyOntoOccupiedSlot_Swaps()
        {
            string armyA = run.armies[0].instanceId;
            string armyB = run.armies[1].instanceId;
            deployment.Place(armyA, 0);
            deployment.Place(armyB, 1);

            deployment.Place(armyA, 1); // 슬롯 간 드래그 — 스왑

            Assert.AreEqual(armyA, deployment.GetArmyAt(1));
            Assert.AreEqual(armyB, deployment.GetArmyAt(0));
        }

        [Test]
        public void Place_FromListOntoOccupiedSlot_DisplacesOccupant()
        {
            string armyA = run.armies[0].instanceId;
            string armyB = run.armies[1].instanceId;
            deployment.Place(armyA, 0);

            deployment.Place(armyB, 0); // 목록 → 점유 슬롯: 기존 부대는 목록으로

            Assert.AreEqual(armyB, deployment.GetArmyAt(0));
            Assert.IsNull(deployment.GetSlotOf(armyA));
            Assert.AreEqual(1, deployment.DeployedCount);
        }

        [Test]
        public void Place_InvalidSlotOrArmy_Throws()
        {
            Assert.Throws<ArgumentException>(() => deployment.Place(run.armies[0].instanceId, 99));
            Assert.Throws<ArgumentException>(() => deployment.Place("", 0));
        }

        [Test]
        public void Place_SameArmyOntoOwnSlot_IsNoOp()
        {
            string army = run.armies[0].instanceId;
            deployment.Place(army, 4);

            deployment.Place(army, 4); // 자기 자신의 슬롯에 재배치

            Assert.AreEqual(army, deployment.GetArmyAt(4));
            Assert.AreEqual(4, deployment.GetSlotOf(army));
            Assert.AreEqual(1, deployment.DeployedCount);
        }

        [Test]
        public void Remove_DeployedArmy_ReturnsToList()
        {
            string army = run.armies[0].instanceId;
            deployment.Place(army, 0);
            deployment.Remove(army);

            Assert.IsNull(deployment.GetArmyAt(0));
            Assert.IsNull(deployment.GetSlotOf(army));
            Assert.AreEqual(0, deployment.DeployedCount);
        }

        [Test]
        public void Remove_ArmyNotDeployed_IsNoOp()
        {
            string army = run.armies[0].instanceId;
            Assert.DoesNotThrow(() => deployment.Remove(army));
            Assert.AreEqual(0, deployment.DeployedCount);
        }

        [Test]
        public void SlotCount_ReflectsGeneratedSlots()
        {
            Assert.AreEqual(9, deployment.SlotCount, "기본 3×3 배치판 = 9슬롯");
        }

        [Test]
        public void IsValidSlot_InRangeAndOutOfRange()
        {
            Assert.IsTrue(deployment.IsValidSlot(0));
            Assert.IsTrue(deployment.IsValidSlot(8));
            Assert.IsFalse(deployment.IsValidSlot(9), "3×3 배치판은 슬롯 0~8만 존재");
            Assert.IsFalse(deployment.IsValidSlot(-1));
        }

        [Test]
        public void Placements_ReflectsCurrentAssignments()
        {
            string armyA = run.armies[0].instanceId;
            string armyB = run.armies[1].instanceId;
            deployment.Place(armyA, 0);
            deployment.Place(armyB, 4);

            Dictionary<string, int> placements = deployment.Placements.ToDictionary(kv => kv.Key, kv => kv.Value);

            Assert.AreEqual(2, placements.Count);
            Assert.AreEqual(0, placements[armyA]);
            Assert.AreEqual(4, placements[armyB]);
        }

        [Test]
        public void CanStartBattle_RequiresAtLeastOneArmy()
        {
            Assert.IsFalse(deployment.CanStartBattle, "빈 배치로는 전투 시작 불가 (§5.7)");
            deployment.Place(run.armies[0].instanceId, 0);
            Assert.IsTrue(deployment.CanStartBattle);
        }

        // ── BattleSetupData 생성 (§7.1) ──────────────────────────────

        [Test]
        public void BuildSetup_ProducesContractFields()
        {
            run.ownedItemIds.Add("item_bow");
            string archer = run.armies[0].instanceId;
            ItemEquipService.Equip(run, archer, "item_bow");
            run.armies[0].bonusSoldierCount = 6;

            deployment.Place(archer, 4);
            deployment.Place(run.armies[1].instanceId, 0);

            BattleSetupData setup = deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs);

            Assert.AreEqual("room_2_0", setup.roomId);
            Assert.AreEqual(RoomType.NormalBattle, setup.roomType);
            Assert.AreEqual("enc_default", setup.encounterId);
            Assert.AreEqual(2, setup.armies.Count);

            DeployedArmy deployed = setup.armies.First(a => a.armyInstanceId == archer);
            Assert.AreEqual("army_basic", deployed.armyDefId);
            Assert.AreEqual(ArmyClass.Archer, deployed.armyClass);
            Assert.AreEqual("item_bow", deployed.equippedItemId);
            Assert.AreEqual("skill_volley", deployed.generalSkillId, "병과 부여 시 장군 스킬 전달 (§4-23)");
            Assert.AreEqual(36, deployed.soldierCount, "기본 30 + 증원 6 (§5.5)");
            Assert.AreEqual(4, deployed.slotId);
            Assert.IsTrue(deployed.slotX > 0f && deployed.slotX < 1f);
            Assert.IsTrue(deployed.slotY > 0f && deployed.slotY < 1f);

            DeployedArmy basic = setup.armies.First(a => a.armyInstanceId != archer);
            Assert.AreEqual(ArmyClass.None, basic.armyClass);
            Assert.IsTrue(string.IsNullOrEmpty(basic.generalSkillId), "기본 군대는 장군 스킬 없음 (§5.5)");
        }

        [Test]
        public void BuildSetup_NonBattleRoomType_Throws()
        {
            deployment.Place(run.armies[0].instanceId, 0);
            Assert.Throws<ArgumentException>(() => deployment.BuildSetup(
                "room_0_3", RoomType.Rest, "enc", run, Items, Defs),
                "배치는 전투/보스 방에서만 (§4-8)");
        }

        [Test]
        public void BuildSetup_EmptyDeployment_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc", run, Items, Defs));
        }

        [Test]
        public void BuildDeployedArmies_EmptyDeployment_ReturnsEmptyList_DoesNotThrow()
        {
            // BuildSetup과 달리 전투 시작 가능 여부/방 타입 제약이 없다 — 배치 화면의 실시간 전투력
            // 미리보기(§4-28)처럼 배치가 비어 있어도 항상 호출 가능해야 한다.
            List<DeployedArmy> result = null;
            Assert.DoesNotThrow(() => result = deployment.BuildDeployedArmies(run, Items, Defs));
            Assert.IsEmpty(result);
        }

        [Test]
        public void BuildDeployedArmies_MatchesBuildSetupArmies()
        {
            deployment.Place(run.armies[0].instanceId, 0);
            deployment.Place(run.armies[1].instanceId, 3);

            List<DeployedArmy> viaHelper = deployment.BuildDeployedArmies(run, Items, Defs);
            BattleSetupData setup = deployment.BuildSetup("room_2_0", RoomType.NormalBattle, "enc", run, Items, Defs);

            CollectionAssert.AreEqual(
                setup.armies.Select(a => (a.armyInstanceId, a.slotId, a.soldierCount)),
                viaHelper.Select(a => (a.armyInstanceId, a.slotId, a.soldierCount)),
                "BuildSetup은 이 헬퍼 결과를 그대로 감싸는 것이어야 함 — 별도 로직이면 어긋날 수 있음");
        }

        [Test]
        public void BuildSetup_OrderIsStableAcrossMovesAndSwaps()
        {
            // 여러 이동/스왑을 거쳐도 결과 리스트는 slotId 오름차순으로 고정돼야 한다 (§7.1 계약 안정성)
            string a = run.armies[0].instanceId;
            string b = run.armies[1].instanceId;
            string c = run.armies[2].instanceId;

            deployment.Place(a, 5);
            deployment.Place(b, 1);
            deployment.Place(c, 3);
            deployment.Place(a, 1); // a↔b 스왑 → a:1, b:5
            deployment.Place(c, 3); // 자기 슬롯 재배치 (no-op)

            BattleSetupData setup = deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc", run, Items, Defs);

            CollectionAssert.AreEqual(
                new[] { 1, 3, 5 }, setup.armies.Select(x => x.slotId).ToArray());
        }
    }
}
