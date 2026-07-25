using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Maps;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 적 군대 구성 자동 생성 (§4-28) — 일반전투는 층수 기반으로 개수를 산출한 뒤 가중 랜덤으로
    /// 병과를 배정하고, 보스는 고정 구성을 그대로 반환한다. RoomTypeAssigner.WeightedPick과 동일한
    /// 가중 랜덤 알고리즘 구조를 재사용한다. 아웃게임 내부 전용(아이템 드롭·전투력 표시용).
    ///
    /// 각 결과는 플레이어의 ArmyInstance와 동일한 형태(ArmyDefinition 참조 + 병과 + 병사 수)를
    /// 갖는다 — <paramref name="template"/>(현재는 army_basic 하나뿐)의 baseSoldierCount/generalPower를
    /// 그대로 물려받는다. 실제 RoomEncounterTable이 여러 ArmyDefinition 중에서 고르게 되면
    /// 이 template 하나만 받던 자리를 풀(pool)로 확장하면 된다.
    /// </summary>
    public static class EnemyCompositionGenerator
    {
        private static readonly ArmyClass[] Classes = { ArmyClass.None, ArmyClass.Archer, ArmyClass.Shieldman };

        public static List<EnemyArmy> Generate(
            int floor, RoomType roomType, EnemyCompositionConfig config, ArmyData template, Random rng)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (floor < 0) throw new ArgumentException($"floor는 0 이상이어야 합니다. 현재: {floor}");

            List<ArmyClass> classes = roomType == RoomType.Boss
                ? new List<ArmyClass>(config.bossComposition)
                : GenerateNormalBattleClasses(floor, config, rng);

            return classes.Select(cls =>
            {
                var enemy = new EnemyArmy
                {
                    armyDefId = template.id,
                    armyClass = cls,
                    soldierCount = template.baseSoldierCount,
                };
                enemy.Validate(); // 잘못된 template(빈 id, 음수 baseSoldierCount 등)을 조용히 넘기지 않는다.
                return enemy;
            }).ToList();
        }

        private static List<ArmyClass> GenerateNormalBattleClasses(int floor, EnemyCompositionConfig config, Random rng)
        {
            int count = Math.Min(config.maxEnemyCount, config.baseEnemyCount + floor * config.perFloorEnemyIncrement);
            var result = new List<ArmyClass>(count);
            for (int i = 0; i < count; i++)
                result.Add(WeightedPick(config, rng));

            return result;
        }

        private static ArmyClass WeightedPick(EnemyCompositionConfig config, Random rng)
        {
            float total = Classes.Sum(config.WeightOf);
            double roll = rng.NextDouble() * total;
            foreach (ArmyClass cls in Classes)
            {
                roll -= config.WeightOf(cls);
                if (roll < 0) return cls;
            }

            return Classes[Classes.Length - 1];
        }
    }
}
