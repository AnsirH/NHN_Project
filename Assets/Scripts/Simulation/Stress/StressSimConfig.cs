namespace NHN.Simulation.Stress
{
    /// <summary>
    /// 스트레스 시뮬 설정. 수치의 단일 출처는 StressTestConfig 에셋(Data 레이어)이며,
    /// 이 struct는 순수 레이어로 복사된 불변 사본이다.
    /// </summary>
    public readonly struct StressSimConfig
    {
        public readonly int TicksPerSecond;
        public readonly float ArenaHalfWidth;
        public readonly float ArenaHalfHeight;
        public readonly float UnitRadius;
        public readonly float MoveSpeed;
        public readonly float KnockbackImpulse;
        public readonly float KnockbackDamping;
        public readonly float RetargetInterval;
        public readonly int MaxUnits;

        public StressSimConfig(
            int ticksPerSecond,
            float arenaHalfWidth,
            float arenaHalfHeight,
            float unitRadius,
            float moveSpeed,
            float knockbackImpulse,
            float knockbackDamping,
            float retargetInterval,
            int maxUnits)
        {
            TicksPerSecond = ticksPerSecond;
            ArenaHalfWidth = arenaHalfWidth;
            ArenaHalfHeight = arenaHalfHeight;
            UnitRadius = unitRadius;
            MoveSpeed = moveSpeed;
            KnockbackImpulse = knockbackImpulse;
            KnockbackDamping = knockbackDamping;
            RetargetInterval = retargetInterval;
            MaxUnits = maxUnits;
        }

        public float TickDeltaTime => 1f / TicksPerSecond;
    }
}
