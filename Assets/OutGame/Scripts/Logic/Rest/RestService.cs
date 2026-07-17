using System;
using OutGame.Logic.Armies;
using OutGame.Logic.Runs;
using UnityEngine;

namespace OutGame.Logic.Rest
{
    /// <summary>
    /// 휴식 방 증원 (§5.5 재정의): 기본 병사 수(baseSoldierCount) 기준 flat +20% 영구 누적.
    /// 병력 손실 모델이 바뀌면 회복 기능으로 환원 가능하도록 별도 서비스로 분리.
    /// </summary>
    public static class RestService
    {
        public const float DefaultReinforcePercent = 0.2f;

        /// <summary>증원을 적용하고 실제로 늘어난 병사 수를 반환한다 (UI 표시용).</summary>
        public static int Reinforce(ArmyInstance army, ArmyData armyDef, float bonusPercent = DefaultReinforcePercent)
        {
            if (army == null) throw new ArgumentNullException(nameof(army));
            if (armyDef == null) throw new ArgumentNullException(nameof(armyDef));
            if (bonusPercent < 0f) throw new ArgumentException($"bonusPercent는 음수일 수 없습니다: {bonusPercent}");

            int amount = Mathf.RoundToInt(armyDef.baseSoldierCount * bonusPercent);
            army.AddBonusSoldiers(amount);
            return amount;
        }
    }
}
