using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 난이도 커브 한 구간 — RunState.powerRoomsVisited가 minCounter 이상이면 이 구간이 적용된다
    /// (2026-07-26 §4-28 재설계). "적 수 + 병과별 가중치 + 보스 구성"을 한 구간에 묶어서, 카운터가
    /// 오를 때마다 셋이 같이 계단식으로 강해지게 한다 — 전부 초안값, 플레이테스트로 조율 예정.
    /// </summary>
    [Serializable]
    public class DifficultyTier
    {
        public int minCounter;
        public int enemyCount = 1;
        public float baseWeight = 1f;
        public float archerWeight;
        public float warriorWeight;
        public float hunterWeight;
        public float assassinWeight;
        public List<ArmyClass> bossComposition = new List<ArmyClass> { ArmyClass.None };

        public DifficultyTier Clone() => new DifficultyTier
        {
            minCounter = minCounter,
            enemyCount = enemyCount,
            baseWeight = baseWeight,
            archerWeight = archerWeight,
            warriorWeight = warriorWeight,
            hunterWeight = hunterWeight,
            assassinWeight = assassinWeight,
            bossComposition = new List<ArmyClass>(bossComposition),
        };

        public void Validate(int index)
        {
            if (minCounter < 0)
                throw new ArgumentException($"tiers[{index}].minCounter는 0 이상이어야 합니다. 현재: {minCounter}");
            if (enemyCount < 1)
                throw new ArgumentException($"tiers[{index}].enemyCount는 1 이상이어야 합니다. 현재: {enemyCount}");
            if (baseWeight <= 0f || archerWeight < 0f || warriorWeight < 0f || hunterWeight < 0f || assassinWeight < 0f)
                throw new ArgumentException(
                    $"tiers[{index}] 병과 가중치는 base > 0, 나머지 ≥ 0 이어야 합니다. " +
                    $"현재: {baseWeight}/{archerWeight}/{warriorWeight}/{hunterWeight}/{assassinWeight}");
            if (bossComposition == null || bossComposition.Count == 0)
                throw new ArgumentException($"tiers[{index}].bossComposition은 비어 있을 수 없습니다.");
        }

        public float WeightOf(ArmyClass armyClass)
        {
            switch (armyClass)
            {
                case ArmyClass.None: return baseWeight;
                case ArmyClass.Archer: return archerWeight;
                case ArmyClass.Warrior: return warriorWeight;
                case ArmyClass.Hunter: return hunterWeight;
                case ArmyClass.Assassin: return assassinWeight;
                default:
                    throw new ArgumentException($"{armyClass} 병과의 적 구성 가중치가 아직 정의되지 않았습니다.");
            }
        }
    }

    /// <summary>
    /// 적 군대 구성 밸런스 값 (§4-28) — RoomEncounterTable이 인게임과 협의되기 전까지의 임시 대체.
    /// 아웃게임 내부 전용(아이템 드롭 계산용) — §7 인터페이스(BattleSetupData)에는 노출하지 않는다.
    ///
    /// 2026-07-26 재설계: "층수(맵 그래프 진행 깊이)"가 아니라 RunState.powerRoomsVisited(증원·증강·
    /// 이벤트 방을 지난 횟수)를 난이도 기준으로 쓴다 — 전투방만 연달아 나오는 런에서 플레이어 보강
    /// 없이 적만 계속 세지는 불균형을 막기 위함(사용자 확정). 적 수·병과 가중치·보스 구성을 tiers
    /// 하나로 묶어 계단식으로 함께 강해지게 한다(마일스톤 방식 — 사용자가 선형보다 선호).
    /// </summary>
    [Serializable]
    public class EnemyCompositionConfig
    {
        // 초안 4단계 — 카운터가 오를수록 적 수 증가 + 병과가 순차 해금(기본만 → +전사·궁수 →
        // +사냥꾼 → +암살자로 전부 해금) + 보스 구성도 그 시점 해금된 병과로 확장. 전부 config,
        // 조정 예정.
        public List<DifficultyTier> tiers = new List<DifficultyTier>
        {
            new DifficultyTier
            {
                // 일반 전투는 이 티어에서 기본 병과만 나오지만(초반 밸런스), 보스는 항상 병과가
                // 있는 유닛을 최소 1개는 포함한다 — 그래야 어느 티어에서 보스를 만나도 전투 승리
                // 아이템 드롭(§4-25)이 완전히 막히지 않는다(보스는 구조상 고정 증원층 직후라
                // powerRoomsVisited가 최소 1은 보장되지만, 이 티어의 상한(minCounter=2 미만)
                // 안에는 여전히 들어올 수 있음).
                minCounter = 0, enemyCount = 2, baseWeight = 1f,
                bossComposition = new List<ArmyClass> { ArmyClass.Warrior, ArmyClass.None, ArmyClass.None },
            },
            new DifficultyTier
            {
                minCounter = 2, enemyCount = 4, baseWeight = 1f, archerWeight = 1f, warriorWeight = 1f,
                bossComposition = new List<ArmyClass>
                {
                    ArmyClass.Warrior, ArmyClass.Warrior, ArmyClass.Archer, ArmyClass.Archer, ArmyClass.None,
                },
            },
            new DifficultyTier
            {
                minCounter = 4, enemyCount = 6, baseWeight = 1f, archerWeight = 1f, warriorWeight = 1f, hunterWeight = 1f,
                bossComposition = new List<ArmyClass>
                {
                    ArmyClass.Warrior, ArmyClass.Warrior, ArmyClass.Archer, ArmyClass.Archer,
                    ArmyClass.Hunter, ArmyClass.Hunter, ArmyClass.None,
                },
            },
            new DifficultyTier
            {
                minCounter = 6, enemyCount = 9,
                baseWeight = 1f, archerWeight = 1f, warriorWeight = 1f, hunterWeight = 1f, assassinWeight = 1f,
                bossComposition = new List<ArmyClass>
                {
                    ArmyClass.Warrior, ArmyClass.Warrior, ArmyClass.Archer, ArmyClass.Archer,
                    ArmyClass.Hunter, ArmyClass.Hunter, ArmyClass.Assassin, ArmyClass.Assassin, ArmyClass.None,
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
