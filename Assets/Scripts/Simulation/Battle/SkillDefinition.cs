namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 플레이어 스킬 1종의 순수 정의 (기획 §8: 쿨다운 기반, 범위 지정 시전).
    /// 즉발(번개)과 장판(독구름)을 하나의 구조로 표현한다 — ZoneDuration 0 이하 = 즉발.
    /// 단일 출처는 SkillData(SO)이며 ToDefinition()으로 변환된다. 스킬별 클래스 금지.
    /// </summary>
    public sealed class SkillDefinition
    {
        public readonly string SkillName;
        public readonly float Cooldown;
        public readonly float Radius;
        /// <summary>시전 순간 범위 내 적에게 1회 적용되는 즉발 데미지 (0 = 없음).</summary>
        public readonly float Damage;
        public readonly StatusEffectType AppliesStatus;
        /// <summary>0 이하 = 상태이상 미부여.</summary>
        public readonly float StatusDuration;
        /// <summary>상태이상 세기 — 도트: 초당 데미지.</summary>
        public readonly float StatusMagnitude;
        /// <summary>0 이하 = 즉발. 양수 = 장판 지속시간 — 지속 동안 매 틱 범위 내 대상에게 상태이상 재부여.</summary>
        public readonly float ZoneDuration;
        /// <summary>true = 아군 대상 (힐 장판/전투 함성 — v4 §9). false = 적군 대상.</summary>
        public readonly bool TargetsAllies;

        public SkillDefinition(
            string skillName, float cooldown, float radius, float damage,
            StatusEffectType appliesStatus, float statusDuration, float statusMagnitude,
            float zoneDuration, bool targetsAllies = false)
        {
            SkillName = skillName;
            Cooldown = cooldown;
            Radius = radius;
            Damage = damage;
            AppliesStatus = appliesStatus;
            StatusDuration = statusDuration;
            StatusMagnitude = statusMagnitude;
            ZoneDuration = zoneDuration;
            TargetsAllies = targetsAllies;
        }
    }
}
