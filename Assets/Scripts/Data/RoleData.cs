using System.Collections.Generic;
using NHN.Simulation.Battle;
using UnityEngine;

namespace NHN.Data
{
    /// <summary>
    /// 롤 1종의 단일 정의 (기획 §10). 롤 추가 = 이 에셋 1개 생성, 코드 수정 0줄.
    /// 시뮬에는 ToDefinition()으로 순수 RoleDefinition만 넘긴다.
    /// v4: 병사 트리거 기믹 필드 제거 — 롤 개성 = 이동 패턴 + 타겟팅 + 스탯 (기획 §7).
    /// </summary>
    [CreateAssetMenu(fileName = "Role", menuName = "NHN/Role")]
    public sealed class RoleData : ScriptableObject
    {
        [Header("스탯")]
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float attackDamage = 10f;
        [Tooltip("피해 감쇠 K/(K+방어력) — K는 BattleConfig.defenseK. 0 = 감쇠 없음")]
        [SerializeField] private float defense;
        [Tooltip("치명타 확률 (0~100 퍼센트 — 아웃게임 표기 단위). 0이면 추첨하지 않는다")]
        [SerializeField] private float critChancePercent;
        [SerializeField] private float attackInterval = 1f;
        [Tooltip("유닛 가장자리 기준 사거리")]
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float unitRadius = 0.5f;

        [Header("타겟팅 (2단계: 위치 필터 → 우선순위 목록)")]
        [SerializeField] private PositionFilter positionFilter = PositionFilter.Nearest;
        [SerializeField] private List<TargetPriority> priorities = new List<TargetPriority>();

        [Header("이동 패턴")]
        [SerializeField] private MovePattern movePattern = MovePattern.ApproachTarget;
        [Tooltip("이동 패턴 파라미터 A — StealthDash: 은신 지속시간(초)")]
        [SerializeField] private float moveParamA;
        [Tooltip("이동 패턴 파라미터 B — StealthDash: 은신 중 이속 배율 (1 = 미적용)")]
        [SerializeField] private float moveParamB = 1f;

        [Header("투사체 (속도 0 = 근접)")]
        [SerializeField] private float projectileSpeed;
        [Tooltip("포물선 정점 높이 — 뷰 표현 전용")]
        [SerializeField] private float projectileArcHeight = 2f;

        [Header("뷰 (가독성 1:1:1 — 롤:실루엣:색)")]
        [SerializeField] private Color roleColor = Color.white;

        public Color RoleColor => roleColor;

        public float UnitRadius => unitRadius;

        public RoleDefinition ToDefinition()
        {
            return new RoleDefinition(
                name,
                maxHp, attackDamage, defense, critChancePercent,
                attackInterval, attackRange,
                moveSpeed, unitRadius,
                projectileSpeed, projectileArcHeight,
                positionFilter, priorities.ToArray(), movePattern, moveParamA, moveParamB);
        }
    }
}
