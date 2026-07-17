using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 아이템 귀속 규칙 검증 (§5.6): 부대당 1개, 회수·교체 불가, 부여로 병과 결정.
    /// </summary>
    public class ItemEquipServiceTests
    {
        private RunState run;

        private static readonly Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>
        {
            ["item_bow"] = new ItemData { id = "item_bow", displayName = "활", armyClass = ArmyClass.Archer, generalSkillId = "skill_volley" },
            ["item_saddle"] = new ItemData { id = "item_saddle", displayName = "안장", armyClass = ArmyClass.Cavalry, generalSkillId = "skill_charge" },
        };

        [SetUp]
        public void SetUp()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), 42).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 2 });
            run.ownedItemIds.Add("item_bow");
        }

        [Test]
        public void Equip_OwnedItemToBasicArmy_BindsAndRemovesFromInventory()
        {
            string armyId = run.armies[0].instanceId;

            Assert.IsTrue(ItemEquipService.CanEquip(run, armyId, "item_bow"));
            ItemEquipService.Equip(run, armyId, "item_bow");

            Assert.AreEqual("item_bow", run.armies[0].EquippedItemId);
            Assert.IsFalse(run.ownedItemIds.Contains("item_bow"), "귀속 — 보유 목록에서 제거돼야 함");
        }

        [Test]
        public void Equip_ArmyAlreadyHasItem_Throws()
        {
            string armyId = run.armies[0].instanceId;
            ItemEquipService.Equip(run, armyId, "item_bow");
            run.ownedItemIds.Add("item_saddle");

            Assert.IsFalse(ItemEquipService.CanEquip(run, armyId, "item_saddle"));
            Assert.Throws<InvalidOperationException>(
                () => ItemEquipService.Equip(run, armyId, "item_saddle"),
                "부대당 1개 — 교체 불가 (§4-6)");
            Assert.AreEqual("item_bow", run.armies[0].EquippedItemId, "기존 귀속이 유지돼야 함");
            Assert.IsTrue(run.ownedItemIds.Contains("item_saddle"), "실패 시 보유 목록 변화 없음");
        }

        [Test]
        public void Equip_UnownedItem_Throws()
        {
            string armyId = run.armies[0].instanceId;

            Assert.IsFalse(ItemEquipService.CanEquip(run, armyId, "item_saddle"));
            Assert.Throws<InvalidOperationException>(
                () => ItemEquipService.Equip(run, armyId, "item_saddle"));
        }

        [Test]
        public void Equip_UnknownArmy_Throws()
        {
            Assert.IsFalse(ItemEquipService.CanEquip(run, "no_such_army", "item_bow"));
            Assert.Throws<InvalidOperationException>(
                () => ItemEquipService.Equip(run, "no_such_army", "item_bow"));
        }

        [Test]
        public void ResolveClass_NoItem_ReturnsNone()
        {
            Assert.AreEqual(ArmyClass.None, ItemEquipService.ResolveClass(run.armies[0], Items));
        }

        [Test]
        public void ResolveClass_EquippedBow_ReturnsArcher()
        {
            ItemEquipService.Equip(run, run.armies[0].instanceId, "item_bow");
            Assert.AreEqual(ArmyClass.Archer, ItemEquipService.ResolveClass(run.armies[0], Items));
        }

        [Test]
        public void ResolveClass_UnknownItemId_Throws()
        {
            run.armies[0].Bind("item_ghost");
            Assert.Throws<ArgumentException>(
                () => ItemEquipService.ResolveClass(run.armies[0], Items),
                "데이터 무결성 — 정의 없는 아이템은 조기에 드러나야 함");
        }
    }
}
