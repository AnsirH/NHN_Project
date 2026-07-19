using System;
using System.Collections.Generic;
using OutGame.Logic.Armies;
using OutGame.Logic.Items;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 군대 정보 팝업 (§5.7 — 배치 화면에서 군대 카드 클릭 시). 좌측 장군 프리뷰/경험치/장군 스탯,
    /// 우측 군대 정보/군대 스탯, 상단 재화 표시.
    /// </summary>
    public class ArmyInfoPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Text currencyLabel;

        [SerializeField] private Image generalPortraitImage;
        [SerializeField] private Text generalNameLabel;
        [SerializeField] private Text expLabel;
        [SerializeField] private Text generalStatsLabel;

        [SerializeField] private Image armyPortraitImage;
        [SerializeField] private Text armyNameLabel;
        [SerializeField] private Text armyStatsLabel;

        private void Awake()
        {
            if (closeButton == null || currencyLabel == null
                || generalPortraitImage == null || generalNameLabel == null || expLabel == null || generalStatsLabel == null
                || armyPortraitImage == null || armyNameLabel == null || armyStatsLabel == null)
                throw new InvalidOperationException("ArmyInfoPopup 프리팹의 필드가 배선되지 않았습니다.");

            closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Hide);
        }

        public void Open(
            ArmyInstance army,
            ArmyDefinition armyDef,
            IReadOnlyDictionary<string, ItemData> itemDataById,
            int gold)
        {
            if (army == null) throw new ArgumentNullException(nameof(army));
            if (armyDef == null) throw new ArgumentNullException(nameof(armyDef));
            if (itemDataById == null) throw new ArgumentNullException(nameof(itemDataById));

            ArmyData data = armyDef.ToData();
            ArmyClass armyClass = ItemEquipService.ResolveClass(army, itemDataById);
            string classLabel = ItemEquipService.ClassDisplayName(armyClass);
            ItemData equipped = ItemEquipService.ResolveItem(army, itemDataById);
            string displayName = ItemEquipService.ResolveDisplayName(army, data.displayName, itemDataById);

            currencyLabel.text = $"재화: {gold}";

            generalPortraitImage.sprite = armyDef.GeneralPortrait;
            generalNameLabel.text = data.generalName;
            // 장군 경험치/성장 규칙은 미결(§5.5/§9) — 표시 영역만 확보, 실제 값은 절대 계산하지 않는다.
            expLabel.text = "경험치: -";
            generalStatsLabel.text = $"전투력 보정: {data.generalPower:0}";

            armyPortraitImage.sprite = armyDef.Portrait;
            armyNameLabel.text = displayName;
            int soldierCount = data.baseSoldierCount + army.bonusSoldierCount;
            string classText = armyClass == ArmyClass.None ? "없음" : classLabel;
            string itemText = equipped == null ? "없음" : equipped.displayName;
            armyStatsLabel.text = $"병사 수: {soldierCount}명\n병과: {classText}\n장착 아이템: {itemText}";

            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 인벤토리 팝업이 열려 있어도 항상 그 위에 떠야 함
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
