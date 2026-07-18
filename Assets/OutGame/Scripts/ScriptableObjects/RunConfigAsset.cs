using OutGame.Logic.Runs;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 런 시작 구성 에셋 (§4-13 — 기본 군대 개수 등, 인스펙터로 조정).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Run Config", fileName = "RunConfig")]
    public class RunConfigAsset : ScriptableObject
    {
        [SerializeField] private ArmyDefinition startingArmy;
        [SerializeField, Min(1)] private int startingArmyCount = 3;
        [SerializeField, Min(0)] private int startingGold = 0;
        [SerializeField, Min(0)] private int battleVictoryGold = 20; // §4-20/§9: 초안값

        public RunConfig ToData()
        {
            if (startingArmy == null)
                throw new System.InvalidOperationException(
                    $"{name}: startingArmy(ArmyDefinition)가 배정되지 않았습니다.");

            return new RunConfig
            {
                startingArmyDefId = startingArmy.ToData().id,
                startingArmyCount = startingArmyCount,
                startingGold = startingGold,
                battleVictoryGold = battleVictoryGold,
            };
        }
    }
}
