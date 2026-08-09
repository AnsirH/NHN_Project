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
        Leader = 3,   // 장군 우선 (참수 — 데이터 옵션으로만 사용, 병사 기본값 아님. 기획 §11 미확정)
    }

    /// <summary>
    /// 상태이상 공용 어휘 (기획 §8 — 부정 5종 확정) + 긍정 효과 케이스 (v4: 힐 장판/전투 함성/방진).
    /// 실행은 StatusEffectSystem 하나가 담당한다 (불변조건 5). 긍정 효과도 별도 버프 시스템 없이 같은 경로.
    /// </summary>
    public enum StatusEffectType
    {
        Stun = 0,         // 행동 정지 (이동·공격 불가, 쿨다운 회복은 진행)
        Poison = 1,       // 도트: 세기 = 초당 데미지
        Burn = 2,         // 도트: 세기 = 초당 데미지 (치유 감소는 세부 미확정 — §11)
        Freeze = 3,       // 이동/공속 감소 (실행은 이후 단계)
        Mark = 4,         // 받는 데미지 증가: 세기 = 받는 피해 배율 (>1). 우선 타겟은 TargetPriority.Marked
        HealOverTime = 5, // 긍정: 세기 = 초당 회복량 (힐 장판)
        AttackUp = 6,     // 긍정: 세기 = 공격력 배율 (>1, 전투 함성)
        DamageResist = 7, // 긍정: 세기 = 받는 피해 배율 (<1, 장군 방진)
        AttackDown = 8,   // 부정: 세기 = 공격력 배율 (<1). 원거리 유닛이 근접 공격을 받으면 부여 (2026-08-10)
    }

    /// <summary>이동 패턴. 새 이동 방식은 케이스 추가로 확장한다.</summary>
    public enum MovePattern
    {
        ApproachTarget = 0,
        /// <summary>스폰 시 은신(지속시간 = RoleDefinition.MoveParamA초) 상태로 타겟에게 돌진.
        /// 은신 중에는 적의 타겟 후보와 충돌 분리에서 제외된다. 시간 만료 또는 첫 공격으로 해제.</summary>
        StealthDash = 1,
    }

    /// <summary>
    /// 트리거 기믹의 발동 조건. v4: 병사 트리거 기믹 폐지로 현재 참조처 없음 —
    /// 기획 §6 구현 노트에 따라 시스템은 존치한다 (장군 시스템 확장 대비 어휘).
    /// 장군 액티브의 발동은 ChargeCondition(충전식)이 담당한다.
    /// </summary>
    public enum GimmickTrigger
    {
        None = 0,
        AlliesDeadNearby = 1,   // paramA=사망 수, paramB=반경
        EveryNthAttack = 2,     // paramA=N
        HealthBelowPercent = 3, // paramA=비율(0~1)
        StealthBreak = 4,       // 은신 해제 시 (시간 만료 또는 첫 공격, 둘 중 먼저)
        TargetHasStatus = 5,    // 공격 대상이 상태이상 보유 시
    }

    /// <summary>
    /// 트리거 기믹의 효과 어휘. v4: 병사 케이스(1~5)는 참조처 없이 존치, 부대 스코프 케이스(10~)를
    /// 장군 액티브 스킬(GeneralDefinition)이 재사용한다 — 기획 §6 "GimmickTrigger/Effect 시스템 재사용".
    /// </summary>
    public enum GimmickEffect
    {
        None = 0,
        ShieldInvulnerable = 1, // paramA=지속시간
        ArrowRain = 2,          // paramA=반경
        Berserk = 3,
        NextAttackCrit = 4,     // paramA=데미지 배율
        DamageMultiplier = 5,   // paramA=데미지 배율

        // ── 부대 스코프 (장군 액티브 전용, 기획 §6 확정 4종) ──
        /// <summary>방진: 부대 전원 받는 피해 감소. paramA=받는 피해 배율(<1), duration=지속 K초.</summary>
        SquadDamageResist = 10,
        /// <summary>일제 사격: 부대 전원 일제 발사 → 적 최대 밀집 지점 화살비. paramA=산포 반경, paramB=화살당 데미지 배율.</summary>
        SquadVolley = 11,
        /// <summary>그림자 습격: 부대 전원 재은신 + 다음 공격 치명타. paramA=은신 지속(초), paramB=치명타 배율.</summary>
        SquadRestealthCrit = 12,
        /// <summary>사냥 선포: 현재 체력 최고 적에게 표식. paramA=표식 세기(받는 피해 배율), duration=표식 지속(초).</summary>
        MarkStrongestEnemy = 13,
    }

    /// <summary>
    /// 장군 액티브 스킬의 충전 조건 (기획 §6 v4: 조건 충전식 — 전투 이벤트가 게이지를 채운다).
    /// 임계치 도달 시 자동 발동 후 리셋 (재충전 가능). 필요량은 GeneralDefinition.ChargeRequired.
    /// </summary>
    public enum ChargeCondition
    {
        None = 0,
        SquadDeaths = 1,  // 부대원 누적 사망 (방진) — 게이지 +1/사망
        SquadAttacks = 2, // 부대 누적 공격 (일제 사격) — 게이지 +1/공격
        SquadKills = 3,   // 부대 누적 킬 (그림자 습격) — 게이지 +1/킬 (마지막 직접 피해 분대 기준)
        TimeElapsed = 4,  // 시간 경과 (사냥 선포) — 게이지 +dt/틱, 필요량 = 초
    }

    /// <summary>
    /// 장군 패시브 (부대 지속 버프, 기획 §6: enum + 수치). 장군 생존 중에만 적용 —
    /// 장군 사망 = 버프 소멸이 곧 부대 약화 (별도 디버프 시스템 금지).
    /// </summary>
    public enum SquadPassive
    {
        None = 0,
        AttackPercent = 1,       // value = 공격력 증가 비율 (0.15 = +15%)
        DamageResistPercent = 2, // value = 받는 피해 감소 비율 (0.15 = -15%)
    }
}
