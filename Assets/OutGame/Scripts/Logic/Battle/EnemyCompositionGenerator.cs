using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Maps;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 적 군대 구성 자동 생성 (§4-28, 2026-08-02 프리셋 조각 기반 재설계) — 일반전투는 난이도 커브
    /// 구간(DifficultyTier)의 등급 가중치로 프리셋을 가중 랜덤으로 개수만큼 뽑고, 보스는 같은 구간의
    /// 고정 프리셋 목록을 그대로 쓴다. 뽑힌 프리셋들의 유닛을 전부 합쳐 최종 구성을 만든다 —
    /// 프리셋 자체는 개발자가 EnemyPresetEditorWindow로 미리 만들어둔 것(수식 역산이 아니라 authored
    /// 데이터)이라, 여기서는 최종 스탯 계산(ArmyStatCalculator, 아군과 동일 공식)만 담당한다.
    /// 여기서 생성된 결과는 아이템 드롭·전투력 표시뿐 아니라 2026-07-29부터 BattleSetupData.enemies로
    /// §7 계약에도 그대로 실린다 — 별도의 인게임 쪽 재생성 없이 이 결과가 곧 실제 전투 스폰 구성이다.
    ///
    /// 2026-07-26 재설계: "층수"가 아니라 <paramref name="powerRoomsVisited"/>(RunState의 난이도
    /// 커브 기준점 — 증원·증강·이벤트 방을 지난 횟수)로 구간을 고른다(§4-28, 사용자 확정).
    /// </summary>
    public static class EnemyCompositionGenerator
    {
        public static List<EnemyArmy> Generate(
            int powerRoomsVisited, RoomType roomType, EnemyCompositionConfig config,
            IReadOnlyDictionary<string, EnemyPresetData> presetsById,
            IReadOnlyDictionary<string, ArmyData> armyDefsById,
            IReadOnlyDictionary<string, AugmentData> augmentDataById,
            Random rng)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (presetsById == null) throw new ArgumentNullException(nameof(presetsById));
            if (armyDefsById == null) throw new ArgumentNullException(nameof(armyDefsById));
            if (augmentDataById == null) throw new ArgumentNullException(nameof(augmentDataById));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (powerRoomsVisited < 0)
                throw new ArgumentException($"powerRoomsVisited는 0 이상이어야 합니다. 현재: {powerRoomsVisited}");

            DifficultyTier tier = config.GetTierFor(powerRoomsVisited);
            List<EnemyPresetData> presets = roomType == RoomType.Boss
                ? ResolveBossPresets(tier, presetsById)
                : PickNormalBattlePresets(tier, presetsById, rng);

            return presets
                .SelectMany(preset => preset.units)
                .Select(unit => BuildEnemyArmy(unit, armyDefsById, augmentDataById))
                .ToList();
        }

        private static List<EnemyPresetData> ResolveBossPresets(
            DifficultyTier tier, IReadOnlyDictionary<string, EnemyPresetData> presetsById)
        {
            var result = new List<EnemyPresetData>(tier.bossPresetIds.Count);
            foreach (string presetId in tier.bossPresetIds)
            {
                if (!presetsById.TryGetValue(presetId, out EnemyPresetData preset))
                    throw new ArgumentException($"보스 구성에 정의된 프리셋 '{presetId}'을 찾을 수 없습니다.");
                result.Add(preset);
            }
            return result;
        }

        private static List<EnemyPresetData> PickNormalBattlePresets(
            DifficultyTier tier, IReadOnlyDictionary<string, EnemyPresetData> presetsById, Random rng)
        {
            var result = new List<EnemyPresetData>(tier.presetCount);
            for (int i = 0; i < tier.presetCount; i++)
            {
                int grade = PickGrade(tier, rng);
                result.Add(PickPresetOfGrade(grade, presetsById, rng));
            }
            return result;
        }

        private static int PickGrade(DifficultyTier tier, Random rng)
        {
            float total = tier.gradeWeights.Sum(g => g.weight);
            double roll = rng.NextDouble() * total;
            foreach (GradeWeight gw in tier.gradeWeights)
            {
                roll -= gw.weight;
                if (roll < 0) return gw.grade;
            }
            return tier.gradeWeights[tier.gradeWeights.Count - 1].grade;
        }

        private static EnemyPresetData PickPresetOfGrade(
            int grade, IReadOnlyDictionary<string, EnemyPresetData> presetsById, Random rng)
        {
            List<EnemyPresetData> pool = presetsById.Values.Where(p => p.grade == grade).ToList();
            if (pool.Count == 0)
                throw new ArgumentException($"{grade} 등급의 프리셋이 하나도 없습니다 — presetsById 확인 필요.");
            return pool[rng.Next(pool.Count)];
        }

        /// <summary>
        /// 프리셋 유닛(입력값) → 최종 EnemyArmy 스탯. DeploymentState.BuildDeployedArmies와 동일한
        /// 공식(ArmyStatCalculator)을 공유해, 프리셋을 만들 때(에디터 미리보기)와 실제 생성 시점의
        /// 계산이 어긋나지 않게 한다.
        /// </summary>
        private static EnemyArmy BuildEnemyArmy(
            EnemyPresetUnit unit, IReadOnlyDictionary<string, ArmyData> armyDefsById,
            IReadOnlyDictionary<string, AugmentData> augmentDataById)
        {
            string armyDefId = ClassArmyDefinitions.DefIdFor(unit.armyClass);
            if (!armyDefsById.TryGetValue(armyDefId, out ArmyData def))
                throw new ArgumentException($"정의되지 않은 ArmyDefinition: {armyDefId}");

            List<AugmentData> selectedAugments = AugmentSelectionResolver.Resolve(unit.selectedAugmentIds, augmentDataById);
            float healthMultiplier = ArmyStatCalculator.GetStatMultiplier(unit.upgradeLevel, unit.armyClass, AugmentStat.Health, selectedAugments);
            float attackMultiplier = ArmyStatCalculator.GetStatMultiplier(unit.upgradeLevel, unit.armyClass, AugmentStat.Attack, selectedAugments);
            float defenseMultiplier = ArmyStatCalculator.GetStatMultiplier(unit.upgradeLevel, unit.armyClass, AugmentStat.Defense, selectedAugments);

            var enemy = new EnemyArmy
            {
                armyDefId = armyDefId,
                armyClass = unit.armyClass,
                soldierCount = unit.soldierCount,
                upgradeLevel = unit.upgradeLevel,
                generalHealth = def.generalHealth * healthMultiplier,
                generalAttack = def.generalAttack * attackMultiplier,
                generalDefense = def.generalDefense * defenseMultiplier,
                generalCritRate = def.generalCritRate,
                generalMoveSpeed = def.generalMoveSpeed,
                soldierHealth = def.soldierHealth * healthMultiplier,
                soldierAttack = def.soldierAttack * attackMultiplier,
                soldierDefense = def.soldierDefense * defenseMultiplier,
            };
            enemy.Validate();
            return enemy;
        }
    }
}
