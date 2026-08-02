using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 생성된 적 군대 1개 (§4-28) — 플레이어의 ArmyInstance와 같은 개념(ArmyDefinition 참조 + 병과 +
    /// 병사 수)이지만, RunState에 저장되는 영구 자산이 아니라 전투 1회용으로 그때그때 생성되는
    /// 임시 데이터다. 아이템/업그레이드/증강 "선택지"는 없지만, 2026-08-02부터 최종 스탯·배치
    /// 좌표는 DeployedArmy와 동일하게 아웃게임이 계산해서 싣는다 — 인게임은 재계산 없이 그대로
    /// 스폰한다. §7 계약(BattleSetupData.enemies)에 그대로 실려 인게임에 전달된다 — 아웃게임이
    /// 확정한 적 구성이 곧 실제 전투에 쓰일 구성이라는 뜻이다(§9 RoomEncounterTable 참고).
    /// </summary>
    [Serializable]
    public class EnemyArmy
    {
        public string armyDefId;
        public ArmyClass armyClass;
        public int soldierCount;

        // DifficultyTier.enemyPowerLevel을 담는 참고용 필드 — DeployedArmy.upgradeLevel과 같은
        // 역할(전투력 재계산 등에 재사용). 최종 스탯 계산엔 이미 반영됨(2026-08-02).
        public int upgradeLevel;

        // 최종 스탯 (DeployedArmy와 동일한 의미 — 업그레이드(난이도) + 병과 보너스 배율이 이미
        // 반영된 값, 2026-08-02).
        public float generalHealth, generalAttack, generalDefense;
        public float generalCritRate, generalMoveSpeed;
        public float soldierHealth, soldierAttack, soldierDefense;

        // 배치 슬롯 — 아웃게임 배치 화면(EnemyFormationAssigner)이 계산한 값을 그대로 옮긴다.
        // DeployedArmy.slotId/slotX/slotY와 동일한 의미(0~1 정규화, 화면에 보이는 배치와 실제
        // 전투 스폰 위치가 항상 같다는 걸 보장하기 위함).
        public int slotId;
        public float slotX;
        public float slotY;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(armyDefId))
                throw new ArgumentException("EnemyArmy.armyDefId가 비어 있습니다.");
            if (soldierCount < 0)
                throw new ArgumentException($"soldierCount는 0 이상이어야 합니다. 현재: {soldierCount}");
        }
    }
}
