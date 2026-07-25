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
    /// 군대 정보 팝업 (§5.7 — 배치 화면에서 군대 카드 클릭 시). 좌측 장군 프리뷰/업그레이드/장군 스탯,
    /// 우측 군대 정보(병사 프리뷰·설명)/군대 스탯, 상단 재화 표시.
    /// 장군 스탯 5종(체력·공격력·방어력·치명타·이동속도)과 군대 스탯 3종(체력·공격력·방어력)은
    /// 아이콘+수치 형태로 표시한다 — 치명타·이동속도는 장군을 따르므로 군대 쪽엔 없다(§5.7 참고 이미지).
    /// 장군 경험치는 제거되고 군대 업그레이드(§4-26)로 대체됐다(2026-07-19) — 업그레이드는 장군·유닛
    /// 체력/공격력/방어력에만 배율로 적용되고 치명타·이동속도는 대상에서 제외한다(밸런스 초안).
    /// </summary>
    public class ArmyInfoPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Text currencyLabel;

        [SerializeField] private Image generalPortraitImage;
        [SerializeField] private Text generalPreviewNameLabel;
        [SerializeField] private Text upgradeLevelLabel;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Text generalHealthLabel;
        [SerializeField] private Text generalAttackLabel;
        [SerializeField] private Text generalDefenseLabel;
        [SerializeField] private Text generalCritRateLabel;
        [SerializeField] private Text generalMoveSpeedLabel;

        [SerializeField] private Image armyPortraitImage;
        [SerializeField] private Text armyNameLabel;
        [SerializeField] private Text soldierCountLabel;
        [SerializeField] private Text armyDescriptionLabel;
        [SerializeField] private Text armySoldierHealthLabel;
        [SerializeField] private Text armySoldierAttackLabel;
        [SerializeField] private Text armySoldierDefenseLabel;

        private ArmyInstance army;
        private ArmyDefinition armyDef;
        private IReadOnlyDictionary<string, ItemData> itemDataById;
        private RunState run;
        private RunConfig runConfig;

        private void Awake()
        {
            if (closeButton == null || currencyLabel == null
                || generalPortraitImage == null || generalPreviewNameLabel == null
                || upgradeLevelLabel == null || upgradeButton == null
                || generalHealthLabel == null || generalAttackLabel == null || generalDefenseLabel == null
                || generalCritRateLabel == null || generalMoveSpeedLabel == null
                || armyPortraitImage == null || armyNameLabel == null || soldierCountLabel == null
                || armyDescriptionLabel == null
                || armySoldierHealthLabel == null || armySoldierAttackLabel == null || armySoldierDefenseLabel == null)
                throw new InvalidOperationException("ArmyInfoPopup 프리팹의 필드가 배선되지 않았습니다.");

            closeButton.onClick.AddListener(Hide);
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Hide);
            upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        }

        public void Open(
            ArmyInstance armyValue,
            ArmyDefinition armyDefValue,
            IReadOnlyDictionary<string, ItemData> itemDataByIdValue,
            RunState runValue,
            RunConfig runConfigValue)
        {
            if (armyValue == null) throw new ArgumentNullException(nameof(armyValue));
            if (armyDefValue == null) throw new ArgumentNullException(nameof(armyDefValue));
            if (itemDataByIdValue == null) throw new ArgumentNullException(nameof(itemDataByIdValue));
            if (runValue == null) throw new ArgumentNullException(nameof(runValue));
            if (runConfigValue == null) throw new ArgumentNullException(nameof(runConfigValue));

            army = armyValue;
            armyDef = armyDefValue;
            itemDataById = itemDataByIdValue;
            run = runValue;
            runConfig = runConfigValue;

            Render();

            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 인벤토리 팝업이 열려 있어도 항상 그 위에 떠야 함
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnUpgradeClicked()
        {
            if (!ArmyUpgradeService.CanUpgrade(army, run, runConfig)) return;
            ArmyUpgradeService.Upgrade(army, run, runConfig);
            Render();
        }

        private void Render()
        {
            ArmyData data = armyDef.ToData();
            string displayName = ItemEquipService.ResolveDisplayName(army, data.displayName, itemDataById);
            float multiplier = ArmyUpgradeService.GetStatMultiplier(army.upgradeLevel);

            currencyLabel.text = $"재화: {run.gold}";

            generalPortraitImage.sprite = armyDef.GeneralPortrait;
            // 병사 프리뷰(soldierCountLabel)와 대응 — 프리뷰 아이콘 아래에 이름을 표시(2026-07-19 사용자
            // 요청). 큰 초상화 박스(GeneralPortrait)는 삭제돼 이 프리뷰 아이콘이 초상화 스프라이트도
            // 겸한다(2026-07-19).
            generalPreviewNameLabel.text = data.generalName;
            upgradeLevelLabel.text = $"+{army.upgradeLevel}";
            upgradeButton.interactable = ArmyUpgradeService.CanUpgrade(army, run, runConfig);
            generalHealthLabel.text = $"{data.generalHealth * multiplier:0}";
            generalAttackLabel.text = $"{data.generalAttack * multiplier:0}";
            generalDefenseLabel.text = $"{data.generalDefense * multiplier:0}";
            generalCritRateLabel.text = $"{data.generalCritRate:0}%";
            generalMoveSpeedLabel.text = $"{data.generalMoveSpeed:0}";

            armyPortraitImage.sprite = armyDef.Portrait;
            armyNameLabel.text = displayName;
            int soldierCount = data.baseSoldierCount + army.bonusSoldierCount;
            soldierCountLabel.text = $"{soldierCount}/{data.maxSoldierCount}명";
            armyDescriptionLabel.text = string.IsNullOrWhiteSpace(data.description) ? "-" : data.description;
            armySoldierHealthLabel.text = $"{data.soldierHealth * multiplier:0}";
            armySoldierAttackLabel.text = $"{data.soldierAttack * multiplier:0}";
            armySoldierDefenseLabel.text = $"{data.soldierDefense * multiplier:0}";
        }
    }
}
