using NHN.Simulation.Battle;
using UnityEngine;

namespace NHN.Data
{
    /// <summary>
    /// 장군 1명의 단일 정의 (기획 §6, v4). 장군 추가 = 이 에셋 1개 생성, 코드 수정 0줄.
    /// 전투 능력은 기반 롤(RoleData) + 엘리트 배율로 파생 — 장군 전용 유닛 클래스 없음.
    /// 시뮬에는 ToDefinition()으로 순수 GeneralDefinition만 넘긴다.
    /// </summary>
    [CreateAssetMenu(fileName = "General", menuName = "NHN/General")]
    public sealed class GeneralData : ScriptableObject
    {
        [Header("전투 능력 (기반 롤 + 엘리트 배율 — 스탯 미지정 시의 파생 규칙)")]
        [SerializeField] private RoleData baseRole;
        [SerializeField] private float hpMultiplier = 3f;
        [SerializeField] private float damageMultiplier = 1.5f;
        [Tooltip("장군 즉시 구분용 크기 배율 (기획 §4) — 시뮬 반경과 뷰 스케일에 함께 적용. 스탯 지정과 무관하게 항상 적용")]
        [SerializeField] private float sizeMultiplier = 1.3f;

        [Header("스탯 (general_stats.csv 임포트 — 레벨 0 스냅샷. maxHp 0이면 위 배율로 파생)")]
        [SerializeField] private float maxHp;
        [SerializeField] private float attackDamage;
        [SerializeField] private float defense;
        [Tooltip("0~100 퍼센트 (아웃게임 표기 단위)")]
        [SerializeField] private float critChancePercent;
        [SerializeField] private float moveSpeed;

        [Header("패시브 (부대 지속 버프 — 장군 생존 중에만, 사망 시 소멸)")]
        [SerializeField] private SquadPassive passive = SquadPassive.AttackPercent;
        [Tooltip("의미는 케이스별 — AttackPercent: 증가 비율 (0.15 = +15%)")]
        [SerializeField] private float passiveValue = 0.15f;

        [Header("액티브 (조건 충전식 — 임계치 도달 시 자동 발동 후 리셋, 전투당 2~3회 튜닝)")]
        [SerializeField] private ChargeCondition chargeCondition = ChargeCondition.TimeElapsed;
        [Tooltip("발동 임계치 — 이벤트형: 횟수, TimeElapsed: 초")]
        [SerializeField] private float chargeRequired = 20f;
        [Tooltip("부대 스코프 케이스만 사용 (SquadDamageResist/SquadVolley/SquadRestealthCrit/MarkStrongestEnemy)")]
        [SerializeField] private GimmickEffect activeEffect = GimmickEffect.SquadDamageResist;
        [SerializeField] private float activeParamA;
        [SerializeField] private float activeParamB;
        [Tooltip("지속형 효과(방진/표식)의 지속시간 초")]
        [SerializeField] private float activeDuration;

        /// <summary>뷰 스케일용 — 시뮬 반경과 동일한 파생 규칙 (기반 롤 × 크기 배율).</summary>
        public float UnitRadius => baseRole.UnitRadius * sizeMultiplier;

        /// <summary>CSV 임포트로 스탯이 지정됐는지 — 체력은 0일 수 없으므로 판정 기준으로 쓴다 (SquadRequest와 같은 규약).</summary>
        public bool HasExplicitStats => maxHp > 0f;

        public float MaxHp => maxHp;

        public float AttackDamage => attackDamage;

        public float Defense => defense;

        public float CritChancePercent => critChancePercent;

        public float MoveSpeed => moveSpeed;

        /// <summary>CSV 임포터가 레벨 0 스냅샷을 써넣는다 (에디터 전용 경로).</summary>
        public void SetStats(float hp, float damage, float armor, float critPercent, float speed)
        {
            maxHp = hp;
            attackDamage = damage;
            defense = armor;
            critChancePercent = critPercent;
            moveSpeed = speed;
        }

        public GeneralDefinition ToDefinition()
        {
            // 엘리트 파생 공식은 GeneralDefinition.CreateElite가 단일 출처 — BalanceLab CLI와 공유 (작업 1).
            GeneralDefinition derived = GeneralDefinition.CreateElite(
                baseRole.ToDefinition(), name,
                hpMultiplier, damageMultiplier, sizeMultiplier,
                passive, passiveValue,
                chargeCondition, chargeRequired,
                activeEffect, activeParamA, activeParamB, activeDuration);
            if (!HasExplicitStats)
            {
                return derived;
            }
            // 스탯이 지정돼 있으면 배율 파생값 대신 그 값을 쓴다 — 반경(크기 배율)은 파생값에서 승계된다.
            return GeneralDefinition.WithCombatRole(
                derived,
                RoleDefinition.WithStats(derived.CombatRole, maxHp, attackDamage, defense, critChancePercent, moveSpeed));
        }
    }
}
