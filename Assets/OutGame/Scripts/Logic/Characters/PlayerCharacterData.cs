using System;

namespace OutGame.Logic.Characters
{
    /// <summary>
    /// 플레이어 캐릭터 데이터 — 캐릭터 선택 화면(§5.2.5)에서 사용. 캐릭터는 스킬을 정확히 하나만
    /// 가지며, 스킬의 세부 효과는 인게임 스킬 시스템 책임이라 여기서는 어떤 스킬인지(skillId)만
    /// 정의한다 — 증강 강화 횟수만 넘기는 §4-27과 동일 원칙.
    /// </summary>
    [Serializable]
    public class PlayerCharacterData
    {
        public string id;
        public string displayName;
        public string description;      // 아웃게임 선택 화면 표시용
        public string skillId;          // §7 계약에 실리는 값 — 인게임 스킬 시스템의 키
        public string skillName;        // 아웃게임 선택 화면 표시용 (placeholder)
        public string skillDescription; // 아웃게임 선택 화면 표시용 (placeholder, 실제 효과는 인게임 책임)

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("PlayerCharacterData.id가 비어 있습니다.");
            if (string.IsNullOrWhiteSpace(skillId))
                throw new ArgumentException($"'{id}': skillId가 비어 있습니다.");
        }
    }
}
