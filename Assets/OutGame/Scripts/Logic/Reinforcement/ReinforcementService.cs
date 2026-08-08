using System;
using OutGame.Logic.Armies;
using OutGame.Logic.Runs;
using UnityEngine;

namespace OutGame.Logic.Reinforcement
{
    /// <summary>
    /// 증원 방 효과 (§5.5 재정의, 2026-07-26 "휴식 방"에서 개칭, 2026-08-08 RestService에서
    /// ReinforcementService로 개명 — Docs/OutGame/화면 명칭 정리.md): 기본 병사 수(baseSoldierCount)
    /// 기준 flat +20% 영구 누적.
    /// 병력 손실 모델이 바뀌면 회복 기능으로 환원 가능하도록 별도 서비스로 분리.
    /// </summary>
    public static class ReinforcementService
    {
        public const float DefaultReinforcePercent = 0.2f;

        /// <summary>
        /// 증원을 적용하고 실제로 늘어난 병사 수를 반환한다 (UI 표시용). 총 병사 수(base+bonus)가
        /// maxSoldierCount(§4-26)를 넘지 않도록 증원량을 잘라낸다 — 이미 상한이면 0을 반환한다.
        /// </summary>
        public static int Reinforce(ArmyInstance army, ArmyData armyDef, float bonusPercent = DefaultReinforcePercent)
        {
            if (army == null) throw new ArgumentNullException(nameof(army));
            if (armyDef == null) throw new ArgumentNullException(nameof(armyDef));
            if (bonusPercent < 0f) throw new ArgumentException($"bonusPercent는 음수일 수 없습니다: {bonusPercent}");

            int desired = Mathf.RoundToInt(armyDef.baseSoldierCount * bonusPercent);
            int currentTotal = armyDef.baseSoldierCount + army.bonusSoldierCount;
            int room = Mathf.Max(0, armyDef.maxSoldierCount - currentTotal);
            int amount = Mathf.Min(desired, room);

            army.AddBonusSoldiers(amount);
            return amount;
        }
    }
}
