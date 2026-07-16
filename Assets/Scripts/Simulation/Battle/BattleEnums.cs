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
    /// (상태이상 케이스는 4단계에서 추가)
    /// </summary>
    public enum TargetPriority
    {
        RangedRole = 0,
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
    }

    /// <summary>트리거 기믹의 효과. 이번 단계는 데이터 정의만 있고 실행은 이후 단계.</summary>
    public enum GimmickEffect
    {
        None = 0,
        ShieldInvulnerable = 1, // paramA=지속시간
        ArrowRain = 2,          // paramA=반경
        Berserk = 3,
        NextAttackCrit = 4,     // paramA=데미지 배율
    }
}
