using NHN.Simulation.Battle;
using UnityEngine;

namespace NHN.Data
{
    /// <summary>
    /// 플레이어 스킬 1종의 단일 정의 (기획 §8). 스킬 추가 = 이 에셋 1개 생성, 코드 수정 0줄.
    /// 시뮬에는 ToDefinition()으로 순수 SkillDefinition만 넘긴다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill", menuName = "NHN/Skill")]
    public sealed class SkillData : ScriptableObject
    {
        [Header("시전 (쿨다운 기반, 범위 지정 — 마나 없음)")]
        [SerializeField] private float cooldown = 8f;
        [SerializeField] private float radius = 3f;
        [Tooltip("시전 순간 범위 내 적에게 1회 (0 = 없음)")]
        [SerializeField] private float damage;

        [Header("상태이상 부여 (지속시간 0 = 미부여)")]
        [SerializeField] private StatusEffectType appliesStatus = StatusEffectType.Stun;
        [SerializeField] private float statusDuration;
        [Tooltip("세기 — 도트(중독/화상): 초당 데미지")]
        [SerializeField] private float statusMagnitude;

        [Header("장판 (0 = 즉발)")]
        [Tooltip("지속 동안 매 틱 범위 내 대상에게 상태이상 재부여")]
        [SerializeField] private float zoneDuration;

        [Header("대상 (v4 §9 — 힐 장판/전투 함성은 아군 대상)")]
        [Tooltip("true = 아군에게 적용 (HealOverTime/AttackUp 등 긍정 효과), false = 적군에게 적용")]
        [SerializeField] private bool targetsAllies;

        [Header("뷰 (스킬 색 = 이펙트/조준 표시 색)")]
        [SerializeField] private Color skillColor = Color.white;
        [Tooltip("HUD 버튼 라벨 — 비우면 에셋 이름 사용")]
        [SerializeField] private string displayName;
        [Tooltip("시전 지점에 재생할 파티클 프리팹 — 비우면 기존 디스크 표시만 사용")]
        [SerializeField] private GameObject castEffectPrefab;
        [Tooltip("이펙트 프리팹이 기본 크기로 덮는 반경(월드 유닛) — 스킬 반경에 맞춰 스케일하는 기준")]
        [SerializeField] private float castEffectBaseRadius = 2.5f;
        [Tooltip("조준(범위 표시) 중 보여줄 프리팹 — 비우면 기존 반투명 원반 표시 사용")]
        [SerializeField] private GameObject rangeIndicatorPrefab;
        [Tooltip("조준 프리팹이 기본 크기로 덮는 반경(월드 유닛) — 스킬 반경에 맞춰 스케일하는 기준")]
        [SerializeField] private float rangeIndicatorBaseRadius = 2.5f;

        public Color SkillColor => skillColor;

        public GameObject CastEffectPrefab => castEffectPrefab;

        public float CastEffectBaseRadius => castEffectBaseRadius;

        public GameObject RangeIndicatorPrefab => rangeIndicatorPrefab;

        public float RangeIndicatorBaseRadius => rangeIndicatorBaseRadius;

        public SkillDefinition ToDefinition()
        {
            return new SkillDefinition(
                string.IsNullOrEmpty(displayName) ? name : displayName,
                cooldown, radius, damage,
                appliesStatus, statusDuration, statusMagnitude,
                zoneDuration, targetsAllies);
        }
    }
}
