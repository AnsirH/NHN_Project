using OutGame.Logic.Battle;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 배치 슬롯 그리드 에셋 (§5.7 — 기본 3×3, 인스펙터에서 조정).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Battle Field Config", fileName = "BattleFieldConfig")]
    public class BattleFieldConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int rows = 3;
        [SerializeField, Min(1)] private int columns = 3;

        public BattleFieldConfigData ToData() => new BattleFieldConfigData { rows = rows, columns = columns };

        private void OnValidate()
        {
            if (rows < 1) rows = 1;
            if (columns < 1) columns = 1;
        }
    }
}
