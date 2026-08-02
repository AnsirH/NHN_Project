using System;
using System.Collections.Generic;
using System.Linq;

namespace OutGame.Logic.Battle
{
    /// <summary>등급 1개에 대한 가중치 — DifficultyTier가 어느 등급의 프리셋 풀에서 뽑을지 정한다.</summary>
    [Serializable]
    public class GradeWeight
    {
        public int grade;
        public float weight = 1f;

        public GradeWeight Clone() => new GradeWeight { grade = grade, weight = weight };
    }

    /// <summary>
    /// 난이도 커브 한 구간 — RunState.powerRoomsVisited가 minCounter 이상이면 이 구간이 적용된다
    /// (2026-07-26 §4-28 재설계, 2026-08-02 프리셋 조각 기반으로 재설계). "프리셋 개수 + 등급별
    /// 가중치 + 보스 프리셋 목록"을 한 구간에 묶어서, 카운터가 오를 때마다 셋이 같이 계단식으로
    /// 강해지게 한다 — 전부 초안값, 플레이테스트로 조율 예정.
    /// </summary>
    [Serializable]
    public class DifficultyTier
    {
        public int minCounter;

        // 이 구간에서 조합할 프리셋 개수(2026-08-02) — 프리셋 하나가 유닛 여러 개를 담을 수 있으므로
        // "적 개체 수"가 아니라 "프리셋 개수"다. 사용자 확정: 목표 전투력 예산 매칭이 아니라 개수 고정.
        public int presetCount = 1;

        // 등급별 가중 랜덤 선택 — 같은 등급 안에서는 프리셋을 균등 랜덤으로 뽑는다.
        public List<GradeWeight> gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 1, weight = 1f } };

        // 보스는 여전히 명시적 고정 조합 — 프리셋 id 참조(EnemyPresetDefinition.presetId).
        public List<string> bossPresetIds = new List<string>();

        public DifficultyTier Clone() => new DifficultyTier
        {
            minCounter = minCounter,
            presetCount = presetCount,
            gradeWeights = gradeWeights.Select(g => g.Clone()).ToList(),
            bossPresetIds = new List<string>(bossPresetIds),
        };

        public void Validate(int index)
        {
            if (minCounter < 0)
                throw new ArgumentException($"tiers[{index}].minCounter는 0 이상이어야 합니다. 현재: {minCounter}");
            if (presetCount < 1)
                throw new ArgumentException($"tiers[{index}].presetCount는 1 이상이어야 합니다. 현재: {presetCount}");
            if (gradeWeights == null || gradeWeights.Count == 0)
                throw new ArgumentException($"tiers[{index}].gradeWeights는 비어 있을 수 없습니다.");
            if (gradeWeights.Any(g => g.weight <= 0f))
                throw new ArgumentException($"tiers[{index}].gradeWeights의 weight는 전부 0보다 커야 합니다.");
            if (bossPresetIds == null || bossPresetIds.Count == 0)
                throw new ArgumentException($"tiers[{index}].bossPresetIds는 비어 있을 수 없습니다.");
        }

        /// <summary>등급의 가중치 — 이 티어에 정의돼 있지 않으면 0(뽑히지 않음).</summary>
        public float WeightOfGrade(int grade) =>
            gradeWeights.FirstOrDefault(g => g.grade == grade)?.weight ?? 0f;
    }

    /// <summary>
    /// 적 군대 구성 밸런스 값 (§4-28) — §9 RoomEncounterTable의 아웃게임 쪽 실제 구현체.
    /// 아이템 드롭 계산에 쓰이는 동시에, 2026-07-29부터 BattleSetupData.enemies로 §7 계약에도
    /// 그대로 실린다 — 인게임과의 밸런스 수치 협의는 계속 필요하지만, 구조 자체는 확정됐다.
    ///
    /// 2026-07-26 재설계: "층수(맵 그래프 진행 깊이)"가 아니라 RunState.powerRoomsVisited(증원·증강·
    /// 이벤트 방을 지난 횟수)를 난이도 기준으로 쓴다 — 전투방만 연달아 나오는 런에서 플레이어 보강
    /// 없이 적만 계속 세지는 불균형을 막기 위함(사용자 확정). 2026-08-02: 적 스탯을 수식으로
    /// 역산하지 않고 개발자가 만든 프리셋 조각(EnemyPresetDefinition)을 등급 기준으로 조합한다 —
    /// 티어별 자동 배율(enemyPowerLevel)이 적 수 증가와 이중으로 곱해지는 문제를 없애기 위함.
    /// </summary>
    [Serializable]
    public class EnemyCompositionConfig
    {
        // 초안 4단계 — 카운터가 오를수록 프리셋 개수 증가 + 등급이 순차 해금. bossPresetIds는
        // 시작 데이터 스크립트(SceneSetupEnemyPresets 등)가 실제로 만든 프리셋 id와 맞춰야 한다.
        public List<DifficultyTier> tiers = new List<DifficultyTier>
        {
            new DifficultyTier
            {
                minCounter = 0, presetCount = 2,
                gradeWeights = new List<GradeWeight> { new GradeWeight { grade = 1, weight = 1f } },
                bossPresetIds = new List<string> { "preset_warrior_g1", "preset_none_g1", "preset_none_g1" },
            },
            new DifficultyTier
            {
                minCounter = 2, presetCount = 3,
                gradeWeights = new List<GradeWeight>
                {
                    new GradeWeight { grade = 1, weight = 1f }, new GradeWeight { grade = 2, weight = 1f },
                },
                bossPresetIds = new List<string>
                {
                    "preset_warrior_g1", "preset_archer_g1", "preset_archer_g1", "preset_none_g1",
                },
            },
            new DifficultyTier
            {
                minCounter = 4, presetCount = 4,
                gradeWeights = new List<GradeWeight>
                {
                    new GradeWeight { grade = 1, weight = 1f }, new GradeWeight { grade = 2, weight = 1f },
                    new GradeWeight { grade = 3, weight = 1f },
                },
                bossPresetIds = new List<string>
                {
                    "preset_warrior_g1", "preset_archer_g1", "preset_hunter_g1", "preset_hunter_g1", "preset_none_g1",
                },
            },
            new DifficultyTier
            {
                minCounter = 6, presetCount = 5,
                gradeWeights = new List<GradeWeight>
                {
                    new GradeWeight { grade = 1, weight = 1f }, new GradeWeight { grade = 2, weight = 1f },
                    new GradeWeight { grade = 3, weight = 1f }, new GradeWeight { grade = 4, weight = 1f },
                },
                bossPresetIds = new List<string>
                {
                    "preset_warrior_g1", "preset_archer_g1", "preset_hunter_g1",
                    "preset_assassin_g1", "preset_assassin_g1", "preset_none_g1",
                },
            },
        };

        /// <summary>필드 단위 얕은 복사 — 호출자가 반환값을 변형해도 원본(에셋 등)에 영향이 없도록 한다.</summary>
        public EnemyCompositionConfig Clone() => new EnemyCompositionConfig
        {
            tiers = tiers.Select(t => t.Clone()).ToList(),
        };

        public void Validate()
        {
            if (tiers == null || tiers.Count == 0)
                throw new ArgumentException("tiers는 비어 있을 수 없습니다.");

            for (int i = 0; i < tiers.Count; i++)
                tiers[i].Validate(i);

            for (int i = 1; i < tiers.Count; i++)
                if (tiers[i].minCounter <= tiers[i - 1].minCounter)
                    throw new ArgumentException(
                        $"tiers는 minCounter 오름차순(중복 없이)이어야 합니다 — tiers[{i - 1}].minCounter=" +
                        $"{tiers[i - 1].minCounter}, tiers[{i}].minCounter={tiers[i].minCounter}");
        }

        /// <summary>
        /// counter 이하인 가장 높은 minCounter 구간을 반환한다. tiers[0].minCounter보다 낮으면
        /// tiers[0]으로 클램프(런 시작 직후 등 카운터가 아직 첫 구간에도 못 미칠 때의 방어적 기본값).
        /// tiers는 minCounter 오름차순이 보장된다(Validate) — 마지막으로 조건을 만족하는 항목을 쓴다.
        /// </summary>
        public DifficultyTier GetTierFor(int counter)
        {
            DifficultyTier result = tiers[0];
            foreach (DifficultyTier tier in tiers)
                if (tier.minCounter <= counter)
                    result = tier;
            return result;
        }
    }
}
