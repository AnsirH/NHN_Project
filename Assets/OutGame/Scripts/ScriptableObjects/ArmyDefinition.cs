using OutGame.Logic.Armies;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 군대 원형 에셋 — ArmyData(순수 로직)를 인스펙터에서 채운다.
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Army Definition", fileName = "ArmyDefinition")]
    public class ArmyDefinition : ScriptableObject
    {
        [SerializeField] private string armyId = "army_basic";
        [SerializeField] private string displayName = "기본 군대";
        [SerializeField] private Sprite portrait;
        [SerializeField, Min(1)] private int baseSoldierCount = 30;

        [Header("장군 (§5.5)")]
        [SerializeField] private string generalName = "이름 없는 장군";
        [SerializeField] private Sprite generalPortrait;
        [SerializeField, Min(0f)] private float generalPower = 10f;

        public Sprite Portrait => portrait;
        public Sprite GeneralPortrait => generalPortrait;

        public ArmyData ToData() => new ArmyData
        {
            id = armyId,
            displayName = displayName,
            baseSoldierCount = baseSoldierCount,
            generalName = generalName,
            generalPower = generalPower,
        };

        private void OnValidate()
        {
            if (baseSoldierCount < 1) baseSoldierCount = 1;
            if (generalPower < 0f) generalPower = 0f;
        }
    }
}
