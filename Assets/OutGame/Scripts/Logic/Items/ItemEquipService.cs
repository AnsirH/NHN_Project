using System;
using System.Collections.Generic;
using OutGame.Logic.Armies;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Items
{
    /// <summary>
    /// 아이템 부여 규칙 (§5.6): 부대당 1개, 귀속(회수·교체 불가), 부여 시 병과 결정.
    /// </summary>
    public static class ItemEquipService
    {
        public static bool CanEquip(RunState run, string armyInstanceId, string itemId)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));

            ArmyInstance army = run.GetArmy(armyInstanceId);
            if (army == null) return false;
            if (army.HasItem) return false; // 부대당 1개 (§4-6)
            return run.ownedItemIds.Contains(itemId);
        }

        /// <summary>부여 실행 — 보유 목록에서 제거되고 부대에 귀속된다. 불가 시 InvalidOperationException.</summary>
        public static void Equip(RunState run, string armyInstanceId, string itemId)
        {
            if (!CanEquip(run, armyInstanceId, itemId))
                throw new InvalidOperationException(
                    $"아이템을 부여할 수 없습니다: army={armyInstanceId}, item={itemId} " +
                    "(부대가 없거나 이미 아이템 보유, 또는 미보유 아이템)");

            ArmyInstance army = run.GetArmy(armyInstanceId);
            army.Bind(itemId);
            run.ownedItemIds.Remove(itemId);
        }

        /// <summary>부여된 아이템 데이터를 조회한다. 아이템 없으면 null.</summary>
        public static ItemData ResolveItem(ArmyInstance army, IReadOnlyDictionary<string, ItemData> items)
        {
            if (army == null) throw new ArgumentNullException(nameof(army));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (!army.HasItem) return null;

            if (!items.TryGetValue(army.EquippedItemId, out ItemData item))
                throw new ArgumentException(
                    $"부대 {army.instanceId}에 정의되지 않은 아이템이 귀속돼 있습니다: {army.EquippedItemId}");

            return item;
        }

        /// <summary>부여된 아이템으로 병과를 판정한다. 아이템 없으면 None.</summary>
        public static ArmyClass ResolveClass(ArmyInstance army, IReadOnlyDictionary<string, ItemData> items)
        {
            ItemData item = ResolveItem(army, items);
            return item?.armyClass ?? ArmyClass.None;
        }

        /// <summary>병과의 한국어 표시명 (§2 용어: 활→궁수, 안장→기마). None이면 빈 문자열.</summary>
        public static string ClassDisplayName(ArmyClass armyClass) => armyClass switch
        {
            ArmyClass.Archer => "궁수",
            ArmyClass.Cavalry => "기마",
            _ => "",
        };
    }
}
