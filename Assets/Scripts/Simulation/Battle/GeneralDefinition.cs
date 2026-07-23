namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 장군 1명의 순수 정의 (기획 §6). 단일 출처는 GeneralData(SO)이며 ToDefinition()으로 변환된다.
    /// 장군별 클래스 금지 — 장군 추가 = GeneralData 에셋 1개.
    /// 구성: 전투 능력(RoleDefinition 재사용) + 패시브(부대 지속 버프: enum + 수치)
    /// + 액티브(충전 조건 enum + 필요량 + GimmickEffect 부대 스코프 케이스 재사용, 자동 발동·재충전).
    /// </summary>
    public sealed class GeneralDefinition
    {
        /// <summary>장군 자신의 유닛 능력 — 병사와 같은 실행 경로를 탄다 (전용 클래스 없음).</summary>
        public readonly RoleDefinition CombatRole;

        public readonly SquadPassive Passive;
        /// <summary>패시브 수치 — 의미는 SquadPassive 케이스별 (AttackPercent: 증가 비율).</summary>
        public readonly float PassiveValue;

        public readonly ChargeCondition ChargeCondition;
        /// <summary>발동 임계치 — 이벤트형: 횟수, TimeElapsed: 초.</summary>
        public readonly float ChargeRequired;
        /// <summary>부대 스코프 효과 (GimmickEffect.Squad* / MarkStrongestEnemy).</summary>
        public readonly GimmickEffect ActiveEffect;
        public readonly float ActiveParamA;
        public readonly float ActiveParamB;
        /// <summary>효과 지속시간(초) — 지속형 효과(방진/표식)에서 사용.</summary>
        public readonly float ActiveDuration;

        public GeneralDefinition(
            RoleDefinition combatRole,
            SquadPassive passive, float passiveValue,
            ChargeCondition chargeCondition, float chargeRequired,
            GimmickEffect activeEffect, float activeParamA, float activeParamB, float activeDuration)
        {
            CombatRole = combatRole;
            Passive = passive;
            PassiveValue = passiveValue;
            ChargeCondition = chargeCondition;
            ChargeRequired = chargeRequired;
            ActiveEffect = activeEffect;
            ActiveParamA = activeParamA;
            ActiveParamB = activeParamB;
            ActiveDuration = activeDuration;
        }
    }
}
