namespace NHN.Simulation.Battle
{
    /// <summary>전투 공통 설정. 수치의 단일 출처는 BattleConfig 에셋(Data 레이어).</summary>
    public readonly struct BattleConfig
    {
        public readonly int TicksPerSecond;
        public readonly float ArenaHalfWidth;
        public readonly float ArenaHalfHeight;
        public readonly float RetargetInterval;
        public readonly float ProjectileImpactRadius;
        public readonly float MaxBattleSeconds;
        public readonly int MaxUnits;
        public readonly int MaxProjectiles;
        /// <summary>동시에 존재할 수 있는 스킬 장판 상한 (고정 배열 크기).</summary>
        public readonly int MaxSkillZones;
        /// <summary>각 군 전선(스폰 기준선)의 중앙으로부터의 거리.</summary>
        public readonly float FrontLineOffsetX;
        /// <summary>정규화 슬롯 slotX 0(후방)~1(전선)이 펼쳐지는 깊이 범위 (DeploymentGrid 변환 파라미터).</summary>
        public readonly float DeploymentDepth;
        /// <summary>정규화 슬롯 slotY 0~1이 펼쳐지는 측면 절반 폭 (중앙 기준 ±).</summary>
        public readonly float DeploymentHalfWidth;

        public BattleConfig(
            int ticksPerSecond, float arenaHalfWidth, float arenaHalfHeight,
            float retargetInterval, float projectileImpactRadius, float maxBattleSeconds,
            int maxUnits, int maxProjectiles, int maxSkillZones, float frontLineOffsetX,
            float deploymentDepth, float deploymentHalfWidth)
        {
            TicksPerSecond = ticksPerSecond;
            ArenaHalfWidth = arenaHalfWidth;
            ArenaHalfHeight = arenaHalfHeight;
            RetargetInterval = retargetInterval;
            ProjectileImpactRadius = projectileImpactRadius;
            MaxBattleSeconds = maxBattleSeconds;
            MaxUnits = maxUnits;
            MaxProjectiles = maxProjectiles;
            MaxSkillZones = maxSkillZones;
            FrontLineOffsetX = frontLineOffsetX;
            DeploymentDepth = deploymentDepth;
            DeploymentHalfWidth = deploymentHalfWidth;
        }

        public float TickDeltaTime => 1f / TicksPerSecond;
    }
}
