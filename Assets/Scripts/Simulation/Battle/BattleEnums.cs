namespace NHN.Simulation.Battle
{
    /// <summary>타겟팅 1단계: 위치 필터. 후보를 위치 기준으로 고른다.</summary>
    public enum PositionFilter
    {
        Nearest = 0,
        Farthest = 1,
    }

    /// <summary>
    /// 타겟팅 2단계: 타입/상태 우선순위. 리스트가 비어 있으면 우선순위 없음.
    /// 앞선 우선순위를 만족하는 후보가 하나라도 있으면 후보를 그들로 좁힌다.
    /// 새 케이스는 AliveEnemyFilter.Accept와 SatisfiesPriority 두 switch에 함께 추가한다.
    /// </summary>
    public enum TargetPriority
    {
        RangedRole = 0,
        Poisoned = 1, // 중독 상태 대상 우선 (사냥꾼 콤보)
        Marked = 2,   // 표식 상태 대상 우선
    }

    /// <summary>
    /// 상태이상 공용 어휘 (기획 §7 — 5종 확정). 실행은 StatusEffectSystem 하나가 담당한다 (불변조건 5).
    /// 도트형(중독/화상)은 세기 = 초당 데미지의 공용 경로 — 새 도트는 enum 케이스 + 분류 한 줄로 동작.
    /// </summary>
    public enum StatusEffectType
    {
        Stun = 0,   // 행동 정지 (이동·공격 불가, 쿨다운 회복은 진행)
        Poison = 1, // 도트: 세기 = 초당 데미지
        Burn = 2,   // 도트: 세기 = 초당 데미지 (치유 감소는 세부 미확정 — §10)
        Freeze = 3, // 이동/공속 감소 (실행은 이후 단계)
        Mark = 4,   // 받는 데미지 증가 (실행은 이후 단계 — 우선 타겟은 TargetPriority.Marked)
    }

    /// <summary>이동 패턴. 새 이동 방식은 케이스 추가로 확장한다.</summary>
    public enum MovePattern
    {
        ApproachTarget = 0,
        /// <summary>스폰 시 은신(지속시간 = RoleDefinition.MoveParamA초) 상태로 타겟에게 돌진.
        /// 은신 중에는 적의 타겟 후보와 충돌 분리에서 제외된다. 시간 만료 또는 첫 공격으로 해제.</summary>
        StealthDash = 1,
    }

    /// <summary>트리거 기믹의 발동 조건 (§6: 롤당 정확히 1개, 조건 트리거만).</summary>
    public enum GimmickTrigger
    {
        None = 0,
        AlliesDeadNearby = 1,   // paramA=사망 수, paramB=반경
        EveryNthAttack = 2,     // paramA=N
        HealthBelowPercent = 3, // paramA=비율(0~1)
        StealthBreak = 4,       // 은신 해제 시 (시간 만료 또는 첫 공격, 둘 중 먼저)
        TargetHasStatus = 5,    // 공격 대상이 상태이상 보유 시 (사냥꾼 콤보 처형)
    }

    /// <summary>트리거 기믹의 효과. 이번 단계는 데이터 정의만 있고 실행은 이후 단계.</summary>
    public enum GimmickEffect
    {
        None = 0,
        ShieldInvulnerable = 1, // paramA=지속시간
        ArrowRain = 2,          // paramA=반경
        Berserk = 3,
        NextAttackCrit = 4,     // paramA=데미지 배율
        DamageMultiplier = 5,   // paramA=데미지 배율 (트리거 조건을 만족한 공격에 적용)
    }
}
