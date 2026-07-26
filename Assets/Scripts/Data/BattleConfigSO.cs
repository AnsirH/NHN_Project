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

        [Header("피해 공식")]
        [Tooltip("방어력 감쇠 계수 K — 피해 × K/(K+방어력). 방어력=K일 때 피해 50% 감소")]
        [SerializeField] private float defenseK = 50f;
        [Tooltip("치명타 피해 배율 (기획 합의: 1.8배)")]
        [SerializeField] private float critMultiplier = 1.8f;

        [Header("장군 스킬 강화 (아웃게임 generalSkillUpgradeCount → 발동 빈도)")]
        [Tooltip("강화 1회당 충전 필요량 감소 비율 (0.15 = -15%)")]
        [SerializeField] private float skillUpgradeChargeReduction = 0.15f;
        [Tooltip("충전 필요량 하한 비율 (0.4 = 기본값의 40%까지만 줄어든다 — 발동이 소음이 되지 않게)")]
        [SerializeField] private float minChargeRequiredRatio = 0.4f;

        [Header("배치 슬롯 변환 (정규화 0~1 → anchor, DeploymentGrid)")]
        [Tooltip("slotX 0(후방)~1(전선)이 펼쳐지는 깊이 범위")]
        [SerializeField] private float deploymentDepth = 10f;
        [Tooltip("slotY 0~1이 펼쳐지는 측면 절반 폭 (중앙 기준 ±)")]
        [SerializeField] private float deploymentHalfWidth = 14f;

        public int Seed => seed;

        public int MaxUnits => maxUnits;

        public int MaxSkillZones => maxSkillZones;

        public BattleConfig ToConfig()
        {
            return new BattleConfig(
                ticksPerSecond, arenaHalfWidth, arenaHalfHeight,
                retargetInterval, projectileImpactRadius, maxBattleSeconds,
                maxUnits, maxProjectiles, maxSkillZones, frontLineOffsetX,
                deploymentDepth, deploymentHalfWidth,
                defenseK, critMultiplier,
                skillUpgradeChargeReduction, minChargeRequiredRatio);
        }
    }
}
