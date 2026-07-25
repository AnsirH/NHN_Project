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

        /// <summary>
        /// 병과의 한국어 표시명 (§2 용어: 활→궁수, 검+방패→전사, 도끼→사냥꾼, 단검→암살자).
        /// 2026-07-26: Warrior의 표시명을 "방패병"에서 "전사"로 개칭(enum/아이템/스킬은 유지,
        /// 브랜딩만 변경) + 사냥꾼(Hunter)·암살자(Assassin) 신규 4병과 체제. None이면 빈 문자열.
        /// </summary>
        public static string ClassDisplayName(ArmyClass armyClass) => armyClass switch
        {
            ArmyClass.Archer => "궁수",
            ArmyClass.Warrior => "전사",
            ArmyClass.Hunter => "사냥꾼",
            ArmyClass.Assassin => "암살자",
            _ => "",
        };

        /// <summary>
        /// 병과 반영 표시명 — 병과 없으면 baseDisplayName 그대로, 있으면 "{병과} 군대" (§2 용어:
        /// 기본 군대 + 활 = 궁수 군대). 이름을 표시하는 화면(배치 UI, 증원 방 등)이 전부 이 메서드로
        /// 통일해야 한다 — 각자 계산하면 한쪽만 고치고 다른 쪽을 놓치기 쉽다(2026-07-19 실제로 발생).
        /// </summary>
        public static string ResolveDisplayName(
            ArmyInstance army, string baseDisplayName, IReadOnlyDictionary<string, ItemData> items)
        {
            ArmyClass armyClass = ResolveClass(army, items);
            return armyClass == ArmyClass.None ? baseDisplayName : $"{ClassDisplayName(armyClass)} 군대";
        }

        /// <summary>
        /// 병과별 대표 아이템 id 매핑을 만든다 (§4-28 아이템 드롭 계산용). 같은 병과에 아이템이
        /// 여럿이면 먼저 등장한 것을 쓴다 — 1차 범위(병과당 아이템 1종)에서는 항상 유일하다.
        /// </summary>
        public static Dictionary<ArmyClass, string> ResolveItemIdByClass(IEnumerable<ItemData> allItems)
        {
            if (allItems == null) throw new ArgumentNullException(nameof(allItems));

            var result = new Dictionary<ArmyClass, string>();
            foreach (ItemData item in allItems)
            {
                if (item.armyClass == ArmyClass.None) continue;
                if (!result.ContainsKey(item.armyClass))
                    result[item.armyClass] = item.id;
            }

            return result;
        }
    }
}
