using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Items
{
    /// <summary>
    /// 아이템 데이터 (순수 POCO — ItemDefinition SO가 이 데이터를 채워 넘긴다).
    /// 아이템 = 병과 = 장군 스킬 세트 (§5.5, §5.6).
    /// </summary>
    [Serializable]
    public class ItemData
    {
        public string id;
        public string displayName;
        public ArmyClass armyClass = ArmyClass.None;
        public string generalSkillId; // 병과 부여 시 장군이 획득하는 스킬 (§4-23, 효과 구현은 인게임)

        // ── 예약 (1차 미사용 — 점토병사식 확장 대비 §5.6) ──
        public string category;
        public string prerequisiteItemId;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ItemData.id가 비어 있습니다.");
        }
    }
}
