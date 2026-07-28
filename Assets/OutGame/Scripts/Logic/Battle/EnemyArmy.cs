using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 생성된 적 군대 1개 (§4-28) — 플레이어의 ArmyInstance와 같은 개념(ArmyDefinition 참조 + 병과 +
    /// 병사 수)이지만, RunState에 저장되는 영구 자산이 아니라 전투 1회용으로 그때그때 생성되는
    /// 임시 데이터다. 아이템/업그레이드/증강은 없음. 2026-07-29부터 §7 계약(BattleSetupData.enemies)에
    /// 그대로 실려 인게임에 전달된다 — 아웃게임이 encounterId 기준으로 확정한 적 구성이 곧 실제
    /// 전투에 쓰일 구성이라는 뜻이다(§9 RoomEncounterTable 참고).
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
