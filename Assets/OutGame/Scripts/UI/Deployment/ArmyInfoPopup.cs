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
    /// 우측 군대 정보(병사 프리뷰·설명)/군대 스탯, 상단 재화 표시.
    /// 장군 스탯 5종(체력·공격력·방어력·치명타·이동속도)과 군대 스탯 3종(체력·공격력·방어력)은
    /// 아이콘+수치 형태로 표시한다 — 치명타·이동속도는 장군을 따르므로 군대 쪽엔 없다(§5.7 참고 이미지).
    /// </summary>
    public class ArmyInfoPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Text currencyLabel;

        [SerializeField] private Image generalPortraitImage;
        [SerializeField] private Text generalNameLabel;
        [SerializeField] private Text expLabel;
        [SerializeField] private Text generalHealthLabel;
        [SerializeField] private Text generalAttackLabel;
        [SerializeField] private Text generalDefenseLabel;
        [SerializeField] private Text generalCritRateLabel;
        [SerializeField] private Text generalMoveSpeedLabel;

        [SerializeField] private Image armyPortraitImage;
        [SerializeField] private Text armyNameLabel;
        [SerializeField] private Text armyDescriptionLabel;
        [SerializeField] private Text armyMetaLabel;
        [SerializeField] private Text armySoldierHealthLabel;
        [SerializeField] private Text armySoldierAttackLabel;
        [SerializeField] private Text armySoldierDefenseLabel;

        private void Awake()
        {
            if (closeButton == null || currencyLabel == null
                || generalPortraitImage == null || generalNameLabel == null || expLabel == null
                || generalHealthLabel == null || generalAttackLabel == null || generalDefenseLabel == null
                || generalCritRateLabel == null || generalMoveSpeedLabel == null
                || armyPortraitImage == null || armyNameLabel == null || armyDescriptionLabel == null
                || armyMetaLabel == null
                || armySoldierHealthLabel == null || armySoldierAttackLabel == null || armySoldierDefenseLabel == null)
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
            string displayName = ItemEquipService.ResolveDisplayName(army, data.displayName, itemDataById);

            currencyLabel.text = $"재화: {gold}";

            generalPortraitImage.sprite = armyDef.GeneralPortrait;
            generalNameLabel.text = data.generalName;
            // 장군 경험치/성장 규칙은 미결(§5.5/§9) — 표시 영역만 확보, 실제 값은 절대 계산하지 않는다.
            expLabel.text = "경험치: -";
            generalHealthLabel.text = $"{data.generalHealth:0}";
            generalAttackLabel.text = $"{data.generalAttack:0}";
            generalDefenseLabel.text = $"{data.generalDefense:0}";
            generalCritRateLabel.text = $"{data.generalCritRate:0}%";
            generalMoveSpeedLabel.text = $"{data.generalMoveSpeed:0}";

            armyPortraitImage.sprite = armyDef.Portrait;
            armyNameLabel.text = displayName;
            armyDescriptionLabel.text = string.IsNullOrWhiteSpace(data.description) ? "-" : data.description;
            // 병과가 곧 장착 아이템을 의미하므로(2026-07-19 사용자 피드백) 병과만 표시한다.
            string classText = armyClass == ArmyClass.None ? "없음" : classLabel;
            armyMetaLabel.text = $"병과: {classText}";
            armySoldierHealthLabel.text = $"{data.soldierHealth:0}";
            armySoldierAttackLabel.text = $"{data.soldierAttack:0}";
            armySoldierDefenseLabel.text = $"{data.soldierDefense:0}";

            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 인벤토리 팝업이 열려 있어도 항상 그 위에 떠야 함
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
