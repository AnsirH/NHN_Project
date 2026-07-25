using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 생성된 적 군대 1개 (§4-28) — 플레이어의 ArmyInstance와 같은 개념(ArmyDefinition 참조 + 병과 +
    /// 병사 수)이지만, RunState에 저장되는 영구 자산이 아니라 전투 1회용으로 그때그때 생성되는
    /// 임시 데이터다. 아이템/업그레이드/증강은 없음 — RoomEncounterTable이 협의되기 전까지의
    /// 아웃게임 내부 전용 대체(§7 인터페이스에는 노출하지 않는다).
    /// </summary>
    [Serializable]
    public class EnemyArmy
    {
        public string armyDefId;
        public ArmyClass armyClass;
        public int soldierCount;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(armyDefId))
                throw new ArgumentException("EnemyArmy.armyDefId가 비어 있습니다.");
            if (soldierCount < 0)
                throw new ArgumentException($"soldierCount는 0 이상이어야 합니다. 현재: {soldierCount}");
        }
    }
}
