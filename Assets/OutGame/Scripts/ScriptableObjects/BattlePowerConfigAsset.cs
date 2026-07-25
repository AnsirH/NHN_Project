using OutGame.Logic.Battle;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 전투력 계수 에셋 (§4-22 — 인게임 스탯 확정 전까지 인스펙터로 튜닝).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Battle Power Config", fileName = "BattlePowerConfig")]
    public class BattlePowerConfigAsset : ScriptableObject
    {
        [SerializeField, Min(0f)] private float baseWeight = 1.0f;
        [SerializeField, Min(0f)] private float archerWeight = 1.2f;
        [SerializeField, Min(0f)] private float shieldmanWeight = 1.5f;

        public BattlePowerConfig ToData() => new BattlePowerConfig
        {
            baseWeight = baseWeight,
            archerWeight = archerWeight,
            shieldmanWeight = shieldmanWeight,
        };
    }
}
