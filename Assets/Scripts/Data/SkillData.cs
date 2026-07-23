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

        public Color SkillColor => skillColor;

        public SkillDefinition ToDefinition()
        {
            return new SkillDefinition(
                name, cooldown, radius, damage,
                appliesStatus, statusDuration, statusMagnitude,
                zoneDuration, targetsAllies);
        }
    }
}
