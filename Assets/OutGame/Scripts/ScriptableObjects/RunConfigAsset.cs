using OutGame.Logic.Armies;
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
        [SerializeField] private ArmyClass startingArmyClass = ArmyClass.None;
        [SerializeField, Min(1)] private int startingArmyCount = 3;
        [SerializeField, Min(0)] private int startingGold = 0;
        [SerializeField, Min(0)] private int battleVictoryGold = 20; // §4-20/§9: 초안값
        [SerializeField, Min(1)] private int maxArmyCount = 28; // §4-7: BattleFieldConfig 슬롯 수와 일치해야 함
        [SerializeField] private int[] armyUpgradeCosts = { 50, 100, 200, 350, 550 }; // §4-26: 초안값

        public RunConfig ToData()
        {
            var data = new RunConfig
            {
                startingArmyClass = startingArmyClass,
                startingArmyCount = startingArmyCount,
                startingGold = startingGold,
                battleVictoryGold = battleVictoryGold,
                maxArmyCount = maxArmyCount,
                armyUpgradeCosts = (int[])armyUpgradeCosts.Clone(), // 배열 참조 공유 방지 — RunConfig 쪽 변경이 에셋에 반영되면 안 됨
            };

            DefinitionValidation.Validate(name, data.Validate);

            return data;
        }
    }
}
