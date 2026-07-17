using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 전투력 계산 계수 (§4-22 초안 — 인게임 스탯 확정 후 조율, 전부 config).
    /// 공식: Σ(병사 수 × 병과 계수) + 장군 보정.
    /// </summary>
    [Serializable]
    public class BattlePowerConfig
    {
        public float baseWeight = 1.0f;
        public float archerWeight = 1.2f;
        public float cavalryWeight = 1.5f;

        public float WeightOf(ArmyClass armyClass)
        {
            switch (armyClass)
            {
                case ArmyClass.None: return baseWeight;
                case ArmyClass.Archer: return archerWeight;
                case ArmyClass.Cavalry: return cavalryWeight;
                default:
                    // Spearman/Shieldman 등 예약 병과 — 계수 미정 상태로 조용히 baseWeight를
                    // 쓰면 활성화 버그를 못 알아챈다 (§4-23 확장 시 반드시 계수 추가 필요).
                    throw new ArgumentException(
                        $"{armyClass} 병과의 전투력 계수가 아직 정의되지 않았습니다 — BattlePowerConfig 확장 필요");
            }
        }
    }
}
