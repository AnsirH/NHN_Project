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

        /// <summary>
        /// 엘리트 파생 공식의 단일 출처 — 장군 전투 능력 = 기반 롤 × 배율 (기획 §5 롤별 장군 스탯 차등).
        /// GeneralData.ToDefinition(Unity)과 BalanceLab CLI가 함께 이 팩토리에 위임한다 (공식 중복 금지).
        ///
        /// 주의(아웃게임 연동 계약): 연결 경로에서는 장군 스탯도 아웃게임이 계산해 전달하므로
        /// hp/damage 배율은 사용되지 않는다 — 이 배율들은 테스트 씬·BalanceLab 로컬 경로 전용이다.
        /// size 배율은 유닛 반경(인게임 소유 속성)이라 연결 경로에서도 계속 쓰인다.
        /// 방어력·치명타는 기반 롤 값을 그대로 승계한다 (엘리트 배율 없음).
        /// </summary>
        public static GeneralDefinition CreateElite(
            RoleDefinition baseRole, string generalName,
            float hpMultiplier, float damageMultiplier, float sizeMultiplier,
            SquadPassive passive, float passiveValue,
            ChargeCondition chargeCondition, float chargeRequired,
            GimmickEffect activeEffect, float activeParamA, float activeParamB, float activeDuration)
        {
            var combatRole = new RoleDefinition(
                generalName,
                baseRole.MaxHp * hpMultiplier,
                baseRole.AttackDamage * damageMultiplier,
                baseRole.Defense,
                baseRole.CritChancePercent,
                baseRole.AttackInterval,
                baseRole.AttackRange,
                baseRole.MoveSpeed,
                baseRole.UnitRadius * sizeMultiplier,
                baseRole.ProjectileSpeed,
                baseRole.ProjectileArcHeight,
                baseRole.PositionFilter,
                baseRole.Priorities,
                baseRole.MovePattern,
                baseRole.MoveParamA,
                baseRole.MoveParamB);
            return new GeneralDefinition(
                combatRole, passive, passiveValue,
                chargeCondition, chargeRequired,
                activeEffect, activeParamA, activeParamB, activeDuration);
        }

        /// <summary>
        /// 능력(패시브·충전·액티브)은 그대로 두고 전투 능력만 교체한 새 정의 — 아웃게임이 전달한 장군 스탯 결합용.
        /// 같은 장군 에셋이 여러 분대에 쓰이면서 분대마다 레벨(스탯)이 다를 수 있으므로 인스턴스를 분리한다.
        /// </summary>
        public static GeneralDefinition WithCombatRole(GeneralDefinition source, RoleDefinition combatRole)
        {
            return new GeneralDefinition(
                combatRole,
                source.Passive, source.PassiveValue,
                source.ChargeCondition, source.ChargeRequired,
                source.ActiveEffect, source.ActiveParamA, source.ActiveParamB, source.ActiveDuration);
        }
    }
}
