using System;
using System.Collections.Generic;
using System.Linq;

namespace OutGame.Logic.Battle
{
    /// <summary>등급 1개의 요구 전투력 범위 — 프리셋을 만들 때 참고/검증용(§EnemyPresetEditorWindow).</summary>
    [Serializable]
    public class PresetGradeRequirement
    {
        public int grade;
        public float minPower;
        public float maxPower;
        /// <summary>등급의 의미(예: "일반전투 초반", "보스급") — 에디터가 등급 선택 옆에 그대로 보여준다.</summary>
        public string description = "";

        public PresetGradeRequirement Clone() => new PresetGradeRequirement
        {
            grade = grade,
            minPower = minPower,
            maxPower = maxPower,
            description = description,
        };
    }

    /// <summary>
    /// 프리셋 등급별 요구 전투력 범위 (2026-08-02) — 개발자가 프리셋 조각을 만들 때 "이 등급이면
    /// 전투력이 대략 얼마여야 하는지" 참고하는 authoring-time 전용 데이터. 런타임 생성 로직
    /// (EnemyCompositionGenerator)은 이 값을 쓰지 않는다 — 등급은 이미 프리셋에 박혀 있고,
    /// 그 등급이 실제로 이 범위를 만족하는지는 만들 때 EnemyPresetEditorWindow가 보여준다.
    /// </summary>
    [Serializable]
    public class PresetGradeConfig
    {
        public List<PresetGradeRequirement> requirements = new List<PresetGradeRequirement>();

        /// <summary>해당 등급의 요구 범위 — 정의돼 있지 않으면 null(에디터가 "기준 없음"으로 표시).</summary>
        public PresetGradeRequirement RequirementFor(int grade) =>
            requirements.FirstOrDefault(r => r.grade == grade);

        public PresetGradeConfig Clone() => new PresetGradeConfig
        {
            requirements = requirements.Select(r => r.Clone()).ToList(),
        };

        public void Validate()
        {
            if (requirements == null)
                throw new ArgumentException("requirements는 null일 수 없습니다(빈 리스트는 허용).");

            var duplicated = requirements.GroupBy(r => r.grade).FirstOrDefault(g => g.Count() > 1);
            if (duplicated != null)
                throw new ArgumentException($"{duplicated.Key} 등급 기준이 중복 정의되어 있습니다.");

            foreach (PresetGradeRequirement req in requirements)
                if (req.maxPower < req.minPower)
                    throw new ArgumentException(
                        $"{req.grade} 등급: maxPower({req.maxPower})가 minPower({req.minPower})보다 작을 수 없습니다.");
        }
    }
}
