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
        [SerializeField, Min(1)] private int maxSoldierCount = 60; // §4-26: 초안 baseSoldierCount×2

        [Header("장군 (§5.5)")]
        [SerializeField] private string generalName = "이름 없는 장군";
        [SerializeField] private Sprite generalPortrait;
        [SerializeField, Min(0f)] private float generalPower = 10f;

        [Header("장군 스탯 (§5.7, §9 미결 — 밸런스 초안값)")]
        [SerializeField, Min(0f)] private float generalHealth = 100f;
        [SerializeField, Min(0f)] private float generalAttack = 10f;
        [SerializeField, Min(0f)] private float generalDefense = 5f;
        [SerializeField, Range(0f, 100f)] private float generalCritRate = 5f;
        [SerializeField, Min(0f)] private float generalMoveSpeed = 100f;

        [Header("군대 정보 (§5.7)")]
        [SerializeField, TextArea] private string description = "";
        [SerializeField, Min(0f)] private float soldierHealth = 50f;
        [SerializeField, Min(0f)] private float soldierAttack = 5f;
        [SerializeField, Min(0f)] private float soldierDefense = 2f;

        public Sprite Portrait => portrait;
        public Sprite GeneralPortrait => generalPortrait;

        public ArmyData ToData()
        {
            var data = new ArmyData
            {
                id = armyId,
                displayName = displayName,
                baseSoldierCount = baseSoldierCount,
                maxSoldierCount = maxSoldierCount,
                generalName = generalName,
                generalPower = generalPower,
                generalHealth = generalHealth,
                generalAttack = generalAttack,
                generalDefense = generalDefense,
                generalCritRate = generalCritRate,
                generalMoveSpeed = generalMoveSpeed,
                description = description,
                soldierHealth = soldierHealth,
                soldierAttack = soldierAttack,
                soldierDefense = soldierDefense,
            };

            try
            {
                data.Validate();
            }
            catch (System.ArgumentException e)
            {
                throw new System.InvalidOperationException($"{name}: 설정값이 유효하지 않습니다 — {e.Message}", e);
            }

            return data;
        }

        private void OnValidate()
        {
            if (baseSoldierCount < 1) baseSoldierCount = 1;
            if (maxSoldierCount < baseSoldierCount) maxSoldierCount = baseSoldierCount;
            if (generalPower < 0f) generalPower = 0f;
            if (generalHealth < 0f) generalHealth = 0f;
            if (generalAttack < 0f) generalAttack = 0f;
            if (generalDefense < 0f) generalDefense = 0f;
            if (generalCritRate < 0f) generalCritRate = 0f;
            if (generalCritRate > 100f) generalCritRate = 100f;
            if (generalMoveSpeed < 0f) generalMoveSpeed = 0f;
            if (soldierHealth < 0f) soldierHealth = 0f;
            if (soldierAttack < 0f) soldierAttack = 0f;
            if (soldierDefense < 0f) soldierDefense = 0f;
        }
    }
}
