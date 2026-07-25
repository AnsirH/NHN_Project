using System;
using UnityEngine;

namespace OutGame.Logic.Runs
{
    /// <summary>
    /// 런 안에서 보유 중인 군대 1개 (상세 기획 §6). 병력 손실 없음 — 부대는 런 내 영구 자산 (§4-15).
    /// equippedItemId는 JsonUtility 직렬화를 위해 필드로 노출하되, 귀속(1회 부여·회수 불가 §5.6)은
    /// Bind()로만 강제한다 — 필드 직접 대입은 이 클래스 밖에서 하지 않는다.
    /// </summary>
    [Serializable]
    public class ArmyInstance
    {
        public const int MaxUpgradeLevel = 5; // §4-26

        public string instanceId;
        public string armyDefId;
        public int bonusSoldierCount;   // 증원 방 보너스 누적 (§5.5) — 음수 금지
        public int upgradeLevel;        // 군대 업그레이드 단계 (§4-26), 0~MaxUpgradeLevel

        [SerializeField] private string equippedItemId; // 비어 있으면 기본 군대

        public string EquippedItemId => equippedItemId;
        public bool HasItem => !string.IsNullOrEmpty(equippedItemId);

        /// <summary>아이템 귀속 — 이미 부여돼 있으면 예외 (§4-6: 교체·회수 불가).</summary>
        public void Bind(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("itemId가 비어 있습니다.", nameof(itemId));
            if (HasItem)
                throw new InvalidOperationException(
                    $"부대 {instanceId}에는 이미 아이템이 귀속돼 있습니다: {equippedItemId}");

            equippedItemId = itemId;
        }

        public void AddBonusSoldiers(int amount)
        {
            if (amount < 0)
                throw new ArgumentException($"증원량은 음수일 수 없습니다: {amount}", nameof(amount));
            bonusSoldierCount += amount;
        }

        /// <summary>업그레이드 1단계 증가 — 이미 최대 단계면 예외 (실제 실행은 ArmyUpgradeService가 재화 차감 후 호출).</summary>
        public void IncrementUpgradeLevel()
        {
            if (upgradeLevel >= MaxUpgradeLevel)
                throw new InvalidOperationException(
                    $"부대 {instanceId}는 이미 최대 업그레이드 단계({MaxUpgradeLevel})입니다.");
            upgradeLevel++;
        }
    }
}
