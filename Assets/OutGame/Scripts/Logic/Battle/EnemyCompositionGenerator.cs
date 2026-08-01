using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Maps;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 적 군대 구성 자동 생성 (§4-28) — 일반전투는 난이도 커브 구간(DifficultyTier)에서 개수를
    /// 가져온 뒤 그 구간의 병과 가중치로 가중 랜덤 배정하고, 보스는 같은 구간의 고정 구성을 그대로
    /// 반환한다. RoomTypeAssigner.WeightedPick과 동일한 가중 랜덤 알고리즘 구조를 재사용한다.
    /// 여기서 생성된 결과는 아이템 드롭·전투력 표시뿐 아니라 2026-07-29부터 BattleSetupData.enemies로
    /// §7 계약에도 그대로 실린다 — 별도의 인게임 쪽 재생성 없이 이 결과가 곧 실제 전투 스폰 구성이다.
    ///
    /// 2026-07-26 재설계: "층수"가 아니라 <paramref name="powerRoomsVisited"/>(RunState의 난이도
    /// 커브 기준점 — 증원·증강·이벤트 방을 지난 횟수)로 구간을 고른다(§4-28, 사용자 확정). 전투방만
    /// 연달아 나오는 런에서 플레이어 보강 없이 적만 계속 세지는 불균형을 막기 위함.
    ///
    /// 각 결과는 플레이어의 ArmyInstance와 동일한 형태(ArmyDefinition 참조 + 병과 + 병사 수)를
    /// 갖는다 — <paramref name="template"/>(현재는 army_basic 하나뿐)의 baseSoldierCount/generalPower를
    /// 그대로 물려받는다. 여러 ArmyDefinition 중에서 고르게 되면 이 template 하나만 받던 자리를
    /// 풀(pool)로 확장하면 된다.
    /// </summary>
    public static class EnemyCompositionGenerator
    {
        private static readonly ArmyClass[] Classes =
        {
            ArmyClass.None, ArmyClass.Archer, ArmyClass.Warrior, ArmyClass.Hunter, ArmyClass.Assassin,
        };

        public static List<EnemyArmy> Generate(
            int powerRoomsVisited, RoomType roomType, EnemyCompositionConfig config, ArmyData template, Random rng)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (powerRoomsVisited < 0)
                throw new ArgumentException($"powerRoomsVisited는 0 이상이어야 합니다. 현재: {powerRoomsVisited}");

            DifficultyTier tier = config.GetTierFor(powerRoomsVisited);
            List<ArmyClass> classes = roomType == RoomType.Boss
                ? new List<ArmyClass>(tier.bossComposition)
                : GenerateNormalBattleClasses(tier, rng);

            return classes.Select(cls =>
            {
                // 스탯은 지금은 배율 없이 template(현재 army_basic = ArmyDefinition_Basic) 원본값을
                // 그대로 옮긴다 — 병과·난이도별 배율은 별도 밸런싱 작업에서 추가 예정(2026-08-02).
                var enemy = new EnemyArmy
                {
                    armyDefId = template.id,
                    armyClass = cls,
                    soldierCount = template.baseSoldierCount,
                    generalHealth = template.generalHealth,
                    generalAttack = template.generalAttack,
                    generalDefense = template.generalDefense,
                    generalCritRate = template.generalCritRate,
                    generalMoveSpeed = template.generalMoveSpeed,
                    soldierHealth = template.soldierHealth,
                    soldierAttack = template.soldierAttack,
                    soldierDefense = template.soldierDefense,
                };
                enemy.Validate(); // 잘못된 template(빈 id, 음수 baseSoldierCount 등)을 조용히 넘기지 않는다.
                return enemy;
            }).ToList();
        }

        private static List<ArmyClass> GenerateNormalBattleClasses(DifficultyTier tier, Random rng)
        {
            // 가중치 합은 같은 티어 안에서 매번 동일하므로 유닛 수만큼 반복하기 전에 한 번만 계산한다.
            float total = Classes.Sum(tier.WeightOf);
            var result = new List<ArmyClass>(tier.enemyCount);
            for (int i = 0; i < tier.enemyCount; i++)
                result.Add(WeightedPick(tier, total, rng));

            return result;
        }

        private static ArmyClass WeightedPick(DifficultyTier tier, float total, Random rng)
        {
            double roll = rng.NextDouble() * total;
            foreach (ArmyClass cls in Classes)
            {
                roll -= tier.WeightOf(cls);
                if (roll < 0) return cls;
            }

            return Classes[Classes.Length - 1];
        }
    }
}
