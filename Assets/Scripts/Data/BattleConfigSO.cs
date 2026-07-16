using NHN.Simulation.Battle;
using UnityEngine;

namespace NHN.Data
{
    /// <summary>전투 공통 수치의 단일 출처. 시뮬에는 ToConfig()로 순수 struct만 넘긴다.</summary>
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "NHN/Battle Config")]
    public sealed class BattleConfigSO : ScriptableObject
    {
        [Header("시뮬레이션")]
        [SerializeField] private int ticksPerSecond = 30;
        [SerializeField] private int maxUnits = 600;
        [SerializeField] private int maxProjectiles = 1024;
        [SerializeField] private int maxSkillZones = 16;
        [SerializeField] private int seed = 20260715;

        [Header("아레나 (시뮬 평면, 월드 x/z)")]
        [SerializeField] private float arenaHalfWidth = 30f;
        [SerializeField] private float arenaHalfHeight = 20f;
        [Tooltip("각 군 전선(스폰 기준선)의 중앙으로부터의 거리")]
        [SerializeField] private float frontLineOffsetX = 12f;

        [Header("전투 규칙")]
        [SerializeField] private float retargetInterval = 0.4f;
        [SerializeField] private float projectileImpactRadius = 0.6f;
        [Tooltip("이 시간까지 승부가 나지 않으면 무승부")]
        [SerializeField] private float maxBattleSeconds = 180f;

        public int Seed => seed;

        public int MaxUnits => maxUnits;

        public int MaxSkillZones => maxSkillZones;

        public BattleConfig ToConfig()
        {
            return new BattleConfig(
                ticksPerSecond, arenaHalfWidth, arenaHalfHeight,
                retargetInterval, projectileImpactRadius, maxBattleSeconds,
                maxUnits, maxProjectiles, maxSkillZones, frontLineOffsetX);
        }
    }
}
