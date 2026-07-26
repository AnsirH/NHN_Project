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
        /// <summary>
        /// 방어력 감쇠 계수 K — 피해 × K/(K+방어력). 방어력 = K일 때 피해 50% 감소.
        /// 밸런싱 손잡이이므로 데이터(BattleConfig 에셋)에 둔다.
        /// </summary>
        public readonly float DefenseK;
        /// <summary>치명타 발생 시 피해 배율 (기획 합의: 1.8배). 전역 상수 — 병과별 차등 없음.</summary>
        public readonly float CritMultiplier;

        public BattleConfig(
            int ticksPerSecond, float arenaHalfWidth, float arenaHalfHeight,
            float retargetInterval, float projectileImpactRadius, float maxBattleSeconds,
            int maxUnits, int maxProjectiles, int maxSkillZones, float frontLineOffsetX,
            float deploymentDepth, float deploymentHalfWidth,
            float defenseK, float critMultiplier)
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
            DefenseK = defenseK;
            CritMultiplier = critMultiplier;
        }

        /// <summary>방어력 감쇠 배율 K/(K+방어력) — 방어력 0이면 1(무감쇠)이라 기존 밸런스가 보존된다.</summary>
        public float DefenseDamping(float defense)
        {
            if (defense <= 0f || DefenseK <= 0f)
            {
                return 1f;
            }
            return DefenseK / (DefenseK + defense);
        }

        public float TickDeltaTime => 1f / TicksPerSecond;
    }
}
