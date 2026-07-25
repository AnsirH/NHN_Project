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
        [SerializeField, Min(0f)] private float warriorWeight = 1.5f;
        [SerializeField, Min(0f)] private float hunterWeight = 1.6f;
        [SerializeField, Min(0f)] private float assassinWeight = 1.4f;

        public BattlePowerConfig ToData() => new BattlePowerConfig
        {
            baseWeight = baseWeight,
            archerWeight = archerWeight,
            warriorWeight = warriorWeight,
            hunterWeight = hunterWeight,
            assassinWeight = assassinWeight,
        };
    }
}
