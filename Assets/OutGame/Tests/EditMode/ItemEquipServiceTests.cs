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
            ["item_shield"] = new ItemData { id = "item_shield", displayName = "검+방패", armyClass = ArmyClass.Warrior, generalSkillId = "skill_taunt" },
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
            ItemEquipService.Equip(run, armyId, "item_bow", Items);

            Assert.AreEqual("item_bow", run.armies[0].EquippedItemId);
            Assert.IsFalse(run.ownedItemIds.Contains("item_bow"), "귀속 — 보유 목록에서 제거돼야 함");
        }

        [Test]
        public void Equip_ArmyAlreadyHasItem_Throws()
        {
            string armyId = run.armies[0].instanceId;
            ItemEquipService.Equip(run, armyId, "item_bow", Items);
            run.ownedItemIds.Add("item_shield");

            Assert.IsFalse(ItemEquipService.CanEquip(run, armyId, "item_shield"));
            Assert.Throws<InvalidOperationException>(
                () => ItemEquipService.Equip(run, armyId, "item_shield", Items),
                "부대당 1개 — 교체 불가 (§4-6)");
            Assert.AreEqual("item_bow", run.armies[0].EquippedItemId, "기존 귀속이 유지돼야 함");
            Assert.IsTrue(run.ownedItemIds.Contains("item_shield"), "실패 시 보유 목록 변화 없음");
        }

        [Test]
        public void Equip_UnownedItem_Throws()
        {
            string armyId = run.armies[0].instanceId;

            Assert.IsFalse(ItemEquipService.CanEquip(run, armyId, "item_shield"));
            Assert.Throws<InvalidOperationException>(
                () => ItemEquipService.Equip(run, armyId, "item_shield", Items));
        }

        [Test]
        public void Equip_UnknownArmy_Throws()
        {
            Assert.IsFalse(ItemEquipService.CanEquip(run, "no_such_army", "item_bow"));
            Assert.Throws<InvalidOperationException>(
                () => ItemEquipService.Equip(run, "no_such_army", "item_bow", Items));
        }

        [Test]
        public void Equip_ItemIdNotInDefinitions_Throws()
        {
            // 보유 목록엔 있지만(예: 저장 데이터 오염) items 딕셔너리에 정의가 없는 경우 — 병과를
            // 확정할 수 없으므로 조기에 드러나야 한다.
            string armyId = run.armies[0].instanceId;
            run.ownedItemIds.Add("item_ghost");

            Assert.Throws<ArgumentException>(
                () => ItemEquipService.Equip(run, armyId, "item_ghost", Items));
        }

        [Test]
        public void ResolveClass_NoItem_ReturnsNone()
        {
            Assert.AreEqual(ArmyClass.None, ItemEquipService.ResolveClass(run.armies[0]));
        }

        [Test]
        public void ResolveClass_EquippedBow_ReturnsArcher()
        {
            ItemEquipService.Equip(run, run.armies[0].instanceId, "item_bow", Items);
            Assert.AreEqual(ArmyClass.Archer, ItemEquipService.ResolveClass(run.armies[0]));
        }

        [Test]
        public void ClassDisplayName_MapsToKorean()
        {
            Assert.AreEqual("", ItemEquipService.ClassDisplayName(ArmyClass.None));
            Assert.AreEqual("궁수", ItemEquipService.ClassDisplayName(ArmyClass.Archer));
            Assert.AreEqual("전사", ItemEquipService.ClassDisplayName(ArmyClass.Warrior));
            Assert.AreEqual("사냥꾼", ItemEquipService.ClassDisplayName(ArmyClass.Hunter));
            Assert.AreEqual("암살자", ItemEquipService.ClassDisplayName(ArmyClass.Assassin));
        }

        [Test]
        public void ResolveItemIdByClass_MapsEachClassToItsItem()
        {
            var result = ItemEquipService.ResolveItemIdByClass(Items.Values);

            Assert.AreEqual("item_bow", result[ArmyClass.Archer]);
            Assert.AreEqual("item_shield", result[ArmyClass.Warrior]);
            Assert.IsFalse(result.ContainsKey(ArmyClass.None), "병과 없는 아이템은 매핑에서 제외");
        }

        [Test]
        public void ResolveItemIdByClass_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ItemEquipService.ResolveItemIdByClass(null));
        }
    }
}
