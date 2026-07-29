using OutGame.Logic.Armies;
using OutGame.Logic.Items;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 아이템 에셋 — ItemData(순수 로직)를 인스펙터에서 채운다. 아이템=병과=장군 스킬 (§5.6).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Item Definition", fileName = "ItemDefinition")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [TextArea] [SerializeField] private string description;

        [Header("병과·장군 스킬 (§5.5, §5.6)")]
        [SerializeField] private ArmyClass armyClass = ArmyClass.None;
        [SerializeField] private string generalSkillId;

        [Header("예약 필드 — 1차 미사용 (§5.6)")]
        [SerializeField] private string category;
        [SerializeField] private ItemDefinition prerequisiteItem;

        public Sprite Icon => icon;
        public string DisplayName => displayName;
        public string Description => description;

        public ItemData ToData()
        {
            var data = new ItemData
            {
                id = itemId,
                displayName = displayName,
                armyClass = armyClass,
                generalSkillId = generalSkillId,
                category = category,
                prerequisiteItemId = prerequisiteItem != null ? prerequisiteItem.itemId : null,
            };

            DefinitionValidation.Validate(name, data.Validate);

            return data;
        }
    }
}
