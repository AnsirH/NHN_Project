using System;
using System.Collections.Generic;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 적 군대 구성 밸런스 값 (§4-28) — RoomEncounterTable이 인게임과 협의되기 전까지의 임시 대체.
    /// 아웃게임 내부 전용(아이템 드롭 계산용) — §7 인터페이스(BattleSetupData)에는 노출하지 않는다.
    /// 일반전투는 층수 기반 자동 생성, 보스는 고정 구성(사용자 확정, 2026-07-19).
    /// </summary>
    [Serializable]
    public class EnemyCompositionConfig
    {
        // 일반전투 — 초안값
        public int baseEnemyCount = 3;
        public int perFloorEnemyIncrement = 1;
        public int maxEnemyCount = 9;
        public float baseWeight = 1.0f;
        public float archerWeight = 1.0f;
        public float shieldmanWeight = 1.0f;

        // 보스 — 고정 구성 (초안값)
        public List<ArmyClass> bossComposition = new List<ArmyClass>
        {
            ArmyClass.Shieldman, ArmyClass.Shieldman, ArmyClass.Archer, ArmyClass.Archer, ArmyClass.None,
        };

        /// <summary>필드 단위 얕은 복사 — 호출자가 반환값을 변형해도 원본(에셋 등)에 영향이 없도록 한다.</summary>
        public EnemyCompositionConfig Clone() => new EnemyCompositionConfig
        {
            baseEnemyCount = baseEnemyCount,
            perFloorEnemyIncrement = perFloorEnemyIncrement,
            maxEnemyCount = maxEnemyCount,
            baseWeight = baseWeight,
            archerWeight = archerWeight,
            shieldmanWeight = shieldmanWeight,
            bossComposition = new List<ArmyClass>(bossComposition),
        };

        public void Validate()
        {
            if (baseEnemyCount < 0)
                throw new ArgumentException($"baseEnemyCount는 0 이상이어야 합니다. 현재: {baseEnemyCount}");
            if (perFloorEnemyIncrement < 0)
                throw new ArgumentException($"perFloorEnemyIncrement는 0 이상이어야 합니다. 현재: {perFloorEnemyIncrement}");
            if (maxEnemyCount < baseEnemyCount)
                throw new ArgumentException(
                    $"maxEnemyCount({maxEnemyCount})는 baseEnemyCount({baseEnemyCount}) 이상이어야 합니다.");
            if (baseWeight <= 0f || archerWeight < 0f || shieldmanWeight < 0f)
                throw new ArgumentException(
                    $"병과 가중치는 base > 0, archer/shieldman ≥ 0 이어야 합니다. 현재: {baseWeight}/{archerWeight}/{shieldmanWeight}");
            if (bossComposition == null || bossComposition.Count == 0)
                throw new ArgumentException("bossComposition은 비어 있을 수 없습니다.");
        }

        public float WeightOf(ArmyClass armyClass)
        {
            switch (armyClass)
            {
                case ArmyClass.None: return baseWeight;
                case ArmyClass.Archer: return archerWeight;
                case ArmyClass.Shieldman: return shieldmanWeight;
                default:
                    throw new ArgumentException($"{armyClass} 병과의 적 구성 가중치가 아직 정의되지 않았습니다.");
            }
        }
    }
}
