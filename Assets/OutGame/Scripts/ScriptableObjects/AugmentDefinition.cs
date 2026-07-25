using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 증강 에셋 — AugmentData(순수 로직)를 인스펙터에서 채운다 (§5.6, §4-27).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Augment Definition", fileName = "AugmentDefinition")]
    public class AugmentDefinition : ScriptableObject
    {
        [SerializeField] private string augmentId;
        [SerializeField] private string displayName;
        [TextArea] [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        [Header("종류 (§5.6)")]
        [SerializeField] private AugmentCategory category;
        [SerializeField] private ArmyClass targetArmyClass = ArmyClass.None; // 아이템 증강일 때만

        [Header("효과")]
        [SerializeField] private AugmentEffectType effectType;
        [SerializeField] private AugmentStat targetStat;     // 스탯 증강일 때만
        [SerializeField, Min(0f)] private float statBoostPercent; // 스탯 증강일 때만 (0.15 = +15%)

        public Sprite Icon => icon;
        public string DisplayName => displayName;
        public string Description => description;

        public AugmentData ToData()
        {
            var data = new AugmentData
            {
                id = augmentId,
                displayName = displayName,
                description = description,
                category = category,
                targetArmyClass = targetArmyClass,
                effectType = effectType,
                targetStat = targetStat,
                statBoostPercent = statBoostPercent,
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
    }
}
