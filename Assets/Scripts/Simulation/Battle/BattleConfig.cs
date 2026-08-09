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
        /// <summary>
        /// 장군 스킬 강화 1회당 충전 필요량 감소 비율 (0.15 = −15%).
        /// 아웃게임의 generalSkillUpgradeCount를 "스킬이 더 자주 발동"으로 번역하는 손잡이 —
        /// 강화 방식의 해석은 인게임 책임이다 (아웃게임 계약 §7.1).
        /// </summary>
        public readonly float SkillUpgradeChargeReduction;
        /// <summary>
        /// 충전 필요량의 하한 비율 (0.4 = 기본값의 40%까지만 줄어든다).
        /// 강화가 쌓여도 액티브가 매 순간 터지는 소음이 되지 않게 막는다 (기획 §6 이벤트 희소성).
        /// </summary>
        public readonly float MinChargeRequiredRatio;
        /// <summary>
        /// 겹침 분리가 시작되는 거리 비율 (반경 합 기준). 1 = 닿는 즉시 분리(하드 제약),
        /// 0.65 = 반경 합의 65% 안까지 파고들어야 분리 — 부분 겹침 허용으로 난전에서
        /// 뒷줄이 앞줄을 밀어내는 현상을 줄인다 (미니워리어즈식 밀집 전투).
        /// </summary>
        public readonly float SeparationOverlapRatio;
        /// <summary>
        /// 틱당 겹침 해소 비율. 1 = 즉시 전량 해소(튕김), 0.2 = 남은 겹침의 20%씩 서서히 —
        /// 밀집 시 떨림·밀림 없이 부드럽게 자리를 잡는다.
        /// </summary>
        public readonly float SeparationStrength;
        /// <summary>
        /// 분대 대형 타이트니스 허용 오차 — 대형 슬롯(앵커+오프셋)과의 거리가 이 안이어야
        /// "정렬됨"으로 본다. 이 값 이내여야 대형 앵커가 전진하거나 Fighting으로 전환한다
        /// (2026-08-09, 분대 대형 이동).
        /// </summary>
        public readonly float FormationTightnessTolerance;
        /// <summary>
        /// 분대 교전 판정 반경의 여유값 — 반경 = 분대 역할군 AttackRange + 대형 반경 + 이 값.
        /// 경계에서 정지·진동하지 않도록 약간의 여유를 둔다 (2026-08-09).
        /// </summary>
        public readonly float FormationEngageRangeMargin;

        public BattleConfig(
            int ticksPerSecond, float arenaHalfWidth, float arenaHalfHeight,
            float retargetInterval, float projectileImpactRadius, float maxBattleSeconds,
            int maxUnits, int maxProjectiles, int maxSkillZones, float frontLineOffsetX,
            float deploymentDepth, float deploymentHalfWidth,
            float defenseK, float critMultiplier,
            float skillUpgradeChargeReduction, float minChargeRequiredRatio,
            float separationOverlapRatio, float separationStrength,
            float formationTightnessTolerance, float formationEngageRangeMargin)
        {
            SkillUpgradeChargeReduction = skillUpgradeChargeReduction;
            MinChargeRequiredRatio = minChargeRequiredRatio;
            SeparationOverlapRatio = separationOverlapRatio;
            SeparationStrength = separationStrength;
            FormationTightnessTolerance = formationTightnessTolerance;
            FormationEngageRangeMargin = formationEngageRangeMargin;
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
