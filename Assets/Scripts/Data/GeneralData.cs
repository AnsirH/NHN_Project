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
        [Header("전투 능력 (기반 롤 + 엘리트 배율 — 롤별 장군 스탯 차등의 근거, 기획 §5)")]
        [SerializeField] private RoleData baseRole;
        [SerializeField] private float hpMultiplier = 3f;
        [SerializeField] private float damageMultiplier = 1.5f;
        [Tooltip("장군 즉시 구분용 크기 배율 (기획 §4) — 시뮬 반경과 뷰 스케일에 함께 적용")]
        [SerializeField] private float sizeMultiplier = 1.3f;

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

        public GeneralDefinition ToDefinition()
        {
            RoleDefinition baseDefinition = baseRole.ToDefinition();
            var combatRole = new RoleDefinition(
                name,
                baseDefinition.MaxHp * hpMultiplier,
                baseDefinition.AttackDamage * damageMultiplier,
                baseDefinition.AttackInterval,
                baseDefinition.AttackRange,
                baseDefinition.MoveSpeed,
                baseDefinition.UnitRadius * sizeMultiplier,
                baseDefinition.ProjectileSpeed,
                baseDefinition.ProjectileArcHeight,
                baseDefinition.PositionFilter,
                baseDefinition.Priorities,
                baseDefinition.MovePattern,
                baseDefinition.MoveParamA,
                baseDefinition.MoveParamB);

            return new GeneralDefinition(
                combatRole,
                passive, passiveValue,
                chargeCondition, chargeRequired,
                activeEffect, activeParamA, activeParamB, activeDuration);
        }
    }
}
