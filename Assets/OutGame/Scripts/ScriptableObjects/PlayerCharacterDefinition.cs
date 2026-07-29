using OutGame.Logic.Characters;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 플레이어 캐릭터 에셋 — PlayerCharacterData(순수 로직)를 인스펙터에서 채운다 (§5.2.5).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Player Character Definition", fileName = "PlayerCharacterDefinition")]
    public class PlayerCharacterDefinition : ScriptableObject
    {
        [SerializeField] private string characterId;
        [SerializeField] private string displayName;
        [TextArea] [SerializeField] private string description;
        [SerializeField] private Sprite portrait;

        [Header("스킬 (§4-2x) — 세부 효과는 인게임 스킬 시스템 책임, 여기서는 어떤 스킬인지만")]
        [SerializeField] private string skillId;
        [SerializeField] private string skillName;
        [TextArea] [SerializeField] private string skillDescription;

        [Header("선택 화면 정렬 순서 (좌→우)")]
        [SerializeField] private int sortOrder;

        public Sprite Portrait => portrait;
        public string DisplayName => displayName;
        public string Description => description;
        public string SkillName => skillName;
        public string SkillDescription => skillDescription;
        public int SortOrder => sortOrder;

        public PlayerCharacterData ToData()
        {
            var data = new PlayerCharacterData
            {
                id = characterId,
                displayName = displayName,
                description = description,
                skillId = skillId,
                skillName = skillName,
                skillDescription = skillDescription,
            };

            DefinitionValidation.Validate(name, data.Validate);

            return data;
        }
    }
}
