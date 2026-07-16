using NHN.Simulation.Stress;
using UnityEngine;

namespace NHN.Data
{
    /// <summary>스트레스 테스트 수치의 단일 출처. 시뮬에는 ToSimConfig()로 순수 struct만 넘긴다.</summary>
    [CreateAssetMenu(fileName = "StressTestConfig", menuName = "NHN/Stress Test Config")]
    public sealed class StressTestConfigSO : ScriptableObject
    {
        [Header("시뮬레이션")]
        [SerializeField] private int ticksPerSecond = 30;
        [SerializeField] private int maxUnits = 500;
        [SerializeField] private int seed = 12345;

        [Header("아레나 (시뮬 평면, 월드 x/z)")]
        [SerializeField] private float arenaHalfWidth = 30f;
        [SerializeField] private float arenaHalfHeight = 20f;

        [Header("유닛")]
        [SerializeField] private float unitRadius = 0.5f;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float knockbackImpulse = 4f;
        [SerializeField] private float knockbackDamping = 6f;
        [SerializeField] private float retargetInterval = 0.4f;

        [Header("측정")]
        [SerializeField] private int targetFrameRate = 120;

        public int MaxUnits => maxUnits;

        public int Seed => seed;

        public int TargetFrameRate => targetFrameRate;

        public StressSimConfig ToSimConfig()
        {
            return new StressSimConfig(
                ticksPerSecond,
                arenaHalfWidth,
                arenaHalfHeight,
                unitRadius,
                moveSpeed,
                knockbackImpulse,
                knockbackDamping,
                retargetInterval,
                maxUnits);
        }
    }
}
