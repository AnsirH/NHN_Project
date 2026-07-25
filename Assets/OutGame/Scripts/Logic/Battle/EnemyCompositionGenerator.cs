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
    /// 가중 랜덤 알고리즘 구조를 재사용한다. 아웃게임 내부 전용(아이템 드롭 계산용).
    /// </summary>
    public static class EnemyCompositionGenerator
    {
        private static readonly ArmyClass[] Classes = { ArmyClass.None, ArmyClass.Archer, ArmyClass.Shieldman };

        public static List<ArmyClass> Generate(int floor, RoomType roomType, EnemyCompositionConfig config, Random rng)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (floor < 0) throw new ArgumentException($"floor는 0 이상이어야 합니다. 현재: {floor}");

            if (roomType == RoomType.Boss)
                return new List<ArmyClass>(config.bossComposition);

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
