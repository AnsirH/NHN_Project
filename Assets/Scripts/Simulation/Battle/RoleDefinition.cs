namespace NHN.Simulation.Battle
{
    /// <summary>트리거 기믹 정의 (조건 + 효과 + 파라미터). 실행은 이후 단계에서 GimmickRunner가 담당한다.</summary>
    public readonly struct GimmickDefinition
    {
        public readonly GimmickTrigger Trigger;
        public readonly float TriggerParamA;
        public readonly float TriggerParamB;
        public readonly GimmickEffect Effect;
        public readonly float EffectParamA;
        public readonly float EffectParamB;

        public GimmickDefinition(
            GimmickTrigger trigger, float triggerParamA, float triggerParamB,
            GimmickEffect effect, float effectParamA, float effectParamB)
        {
            Trigger = trigger;
            TriggerParamA = triggerParamA;
            TriggerParamB = triggerParamB;
            Effect = effect;
            EffectParamA = effectParamA;
            EffectParamB = effectParamB;
        }
    }

    /// <summary>
    /// 롤 1종의 순수 정의. 단일 출처는 RoleData(SO)이며 ToDefinition()으로 변환된다.
    /// 롤별 클래스 금지 — 모든 롤은 이 데이터 하나로 표현된다.
    /// </summary>
    public sealed class RoleDefinition
    {
        public readonly string RoleName;
        public readonly float MaxHp;
        public readonly float AttackDamage;
        public readonly float AttackInterval;
        /// <summary>유닛 가장자리 기준 사거리 (중심 거리 - 양쪽 반경).</summary>
        public readonly float AttackRange;
        public readonly float MoveSpeed;
        public readonly float UnitRadius;
        /// <summary>0 이하 = 근접 즉시 타격, 양수 = 투사체 속도(유닛/초).</summary>
        public readonly float ProjectileSpeed;
        /// <summary>포물선 정점 높이 — 뷰 표현 전용, 시뮬 판정에는 불사용.</summary>
        public readonly float ProjectileArcHeight;
        public readonly PositionFilter PositionFilter;
        public readonly TargetPriority[] Priorities;
        public readonly MovePattern MovePattern;
        /// <summary>이동 패턴 파라미터 A — 의미는 패턴별 (StealthDash: 은신 지속시간 초).</summary>
        public readonly float MoveParamA;
        /// <summary>이동 패턴 파라미터 B — 의미는 패턴별 (StealthDash: 은신 중 이속 배율, 0 이하면 미적용).</summary>
        public readonly float MoveParamB;
        public readonly GimmickDefinition Gimmick;

        public bool IsRanged => ProjectileSpeed > 0f;

        public RoleDefinition(
            string roleName,
            float maxHp, float attackDamage, float attackInterval, float attackRange,
            float moveSpeed, float unitRadius,
            float projectileSpeed, float projectileArcHeight,
            PositionFilter positionFilter, TargetPriority[] priorities, MovePattern movePattern,
            float moveParamA, float moveParamB,
            GimmickDefinition gimmick)
        {
            RoleName = roleName;
            MaxHp = maxHp;
            AttackDamage = attackDamage;
            AttackInterval = attackInterval;
            AttackRange = attackRange;
            MoveSpeed = moveSpeed;
            UnitRadius = unitRadius;
            ProjectileSpeed = projectileSpeed;
            ProjectileArcHeight = projectileArcHeight;
            PositionFilter = positionFilter;
            Priorities = priorities ?? System.Array.Empty<TargetPriority>();
            MovePattern = movePattern;
            MoveParamA = moveParamA;
            MoveParamB = moveParamB;
            Gimmick = gimmick;
        }
    }
}
