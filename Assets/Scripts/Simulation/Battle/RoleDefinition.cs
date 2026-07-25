namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 롤 1종의 순수 정의. 단일 출처는 RoleData(SO)이며 ToDefinition()으로 변환된다.
    /// 롤별 클래스 금지 — 모든 롤은 이 데이터 하나로 표현된다.
    /// v4: 병사 트리거 기믹 폐지 — 롤의 개성은 이동 패턴 + 타겟팅 + 스탯 프로필로만 구성된다 (기획 §7).
    /// 장군의 전투 능력도 이 구조를 재사용한다 (GeneralDefinition.CombatRole).
    /// </summary>
    public sealed class RoleDefinition
    {
        public readonly string RoleName;
        public readonly float MaxHp;
        public readonly float AttackDamage;
        /// <summary>방어력 — 피해 감쇠 계수 K/(K+방어력)로 적용된다 (K = BattleConfig.DefenseK). 0 = 감쇠 없음.</summary>
        public readonly float Defense;
        /// <summary>치명타 확률 (0~100 퍼센트 — 아웃게임 표기 단위와 동일). 0이면 추첨하지 않는다.</summary>
        public readonly float CritChancePercent;
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

        public bool IsRanged => ProjectileSpeed > 0f;

        public RoleDefinition(
            string roleName,
            float maxHp, float attackDamage, float defense, float critChancePercent,
            float attackInterval, float attackRange,
            float moveSpeed, float unitRadius,
            float projectileSpeed, float projectileArcHeight,
            PositionFilter positionFilter, TargetPriority[] priorities, MovePattern movePattern,
            float moveParamA, float moveParamB)
        {
            RoleName = roleName;
            MaxHp = maxHp;
            AttackDamage = attackDamage;
            Defense = defense;
            CritChancePercent = critChancePercent;
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
        }
    }
}
