using System;
using System.Collections.Generic;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Items;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using TMPro;
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
    /// 증강(§4-27)도 장군·유닛 동일하게 적용된다(2026-07-26 사용자 확정 — 이전엔 장군은 업그레이드만
    /// 받도록 돼 있었으나, "장군도 증강으로 강해져야 한다"는 피드백으로 유닛과 동일 배율 공식으로 통일).
    /// </summary>
    public class ArmyInfoPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text currencyLabel;

        [SerializeField] private Image generalPortraitImage;
        [SerializeField] private TMP_Text generalPreviewNameLabel;
        [SerializeField] private TMP_Text upgradeLevelLabel;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text generalHealthLabel;
        [SerializeField] private TMP_Text generalAttackLabel;
        [SerializeField] private TMP_Text generalDefenseLabel;
        [SerializeField] private TMP_Text generalCritRateLabel;
        [SerializeField] private TMP_Text generalMoveSpeedLabel;

        [SerializeField] private Image armyPortraitImage;
        [SerializeField] private TMP_Text armyNameLabel;
        [SerializeField] private TMP_Text soldierCountLabel;
        [SerializeField] private TMP_Text armyDescriptionLabel;
        [SerializeField] private TMP_Text armySoldierHealthLabel;
        [SerializeField] private TMP_Text armySoldierAttackLabel;
        [SerializeField] private TMP_Text armySoldierDefenseLabel;

        private ArmyInstance army;
        private ArmyDefinition armyDef;
        private IReadOnlyDictionary<string, ItemData> itemDataById;
        private IReadOnlyDictionary<string, AugmentData> augmentDataById;
        private RunState run;
        private RunConfig runConfig;
        private TMP_Text upgradeButtonLabel; // Render()에서 최초 1회만 찾아 캐싱 (아래 주석 참고)

        /// <summary>업그레이드로 골드가 차감됐을 때 발행 — 배치 패널 상단 재화 표시 갱신용(2026-07-26).</summary>
        public event Action Upgraded;

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
            RunConfig runConfigValue,
            IReadOnlyDictionary<string, AugmentData> augmentDataByIdValue)
        {
            if (armyValue == null) throw new ArgumentNullException(nameof(armyValue));
            if (armyDefValue == null) throw new ArgumentNullException(nameof(armyDefValue));
            if (itemDataByIdValue == null) throw new ArgumentNullException(nameof(itemDataByIdValue));
            if (runValue == null) throw new ArgumentNullException(nameof(runValue));
            if (runConfigValue == null) throw new ArgumentNullException(nameof(runConfigValue));
            if (augmentDataByIdValue == null) throw new ArgumentNullException(nameof(augmentDataByIdValue));

            army = armyValue;
            armyDef = armyDefValue;
            itemDataById = itemDataByIdValue;
            run = runValue;
            runConfig = runConfigValue;
            augmentDataById = augmentDataByIdValue;

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
            Upgraded?.Invoke();
        }

        private void Render()
        {
            ArmyData data = armyDef.ToData();
            string displayName = ItemEquipService.ResolveDisplayName(army, data.displayName);
            ArmyClass armyClass = ItemEquipService.ResolveClass(army);
            // 장군·유닛 스탯 모두 업그레이드 + 증강(아이템/공통) 합산 — 같은 군대·같은 병과이므로 배율도
            // 동일하다(2026-07-26 사용자 확정). 각자 계산하지 않도록 공용 헬퍼 하나로 통일.
            List<AugmentData> selectedAugments = AugmentSelectionResolver.Resolve(run.selectedAugmentIds, augmentDataById);
            float healthMultiplier = ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, armyClass, AugmentStat.Health, selectedAugments);
            float attackMultiplier = ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, armyClass, AugmentStat.Attack, selectedAugments);
            float defenseMultiplier = ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, armyClass, AugmentStat.Defense, selectedAugments);

            currencyLabel.text = $"재화: {run.gold}";

            generalPortraitImage.sprite = armyDef.GeneralPortrait;
            // 병사 프리뷰(soldierCountLabel)와 대응 — 프리뷰 아이콘 아래에 이름을 표시(2026-07-19 사용자
            // 요청). 큰 초상화 박스(GeneralPortrait)는 삭제돼 이 프리뷰 아이콘이 초상화 스프라이트도
            // 겸한다(2026-07-19).
            generalPreviewNameLabel.text = data.generalName;
            upgradeLevelLabel.text = $"+{army.upgradeLevel}";
            upgradeButton.interactable = ArmyUpgradeService.CanUpgrade(army, run, runConfig);
            // 다음 단계 비용 표시(2026-07-26 사용자 요청) — 최대 단계면 비용 자체가 없으므로 MAX 표시.
            // 팝업이 처음 열릴 때는 아직 비활성 상태에서 Render()가 먼저 호출되는데(Open() 참고),
            // Unity는 비활성 GameObject의 Awake()를 활성화 시점까지 미루므로 Awake()에서 캐싱해두면
            // 첫 오픈 때 null이 된다 — 최초 1회만 Render() 안에서 찾고, 이후엔 캐싱된 필드를 재사용한다.
            if (upgradeButtonLabel == null)
            {
                upgradeButtonLabel = upgradeButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
                if (upgradeButtonLabel == null)
                    throw new InvalidOperationException("ArmyInfoPopup의 upgradeButton에 라벨 Text가 없습니다.");
            }
            upgradeButtonLabel.text = army.upgradeLevel >= ArmyInstance.MaxUpgradeLevel
                ? "MAX"
                : $"업그레이드 ({ArmyUpgradeService.NextUpgradeCost(army, runConfig)})";
            generalHealthLabel.text = $"{data.generalHealth * healthMultiplier:0}";
            generalAttackLabel.text = $"{data.generalAttack * attackMultiplier:0}";
            generalDefenseLabel.text = $"{data.generalDefense * defenseMultiplier:0}";
            generalCritRateLabel.text = $"{data.generalCritRate:0}%";
            generalMoveSpeedLabel.text = $"{data.generalMoveSpeed:0}";

            armyPortraitImage.sprite = armyDef.Portrait;
            armyNameLabel.text = displayName;
            int soldierCount = data.baseSoldierCount + army.bonusSoldierCount;
            soldierCountLabel.text = $"{soldierCount}/{data.maxSoldierCount}명";
            armyDescriptionLabel.text = string.IsNullOrWhiteSpace(data.description) ? "-" : data.description;
            armySoldierHealthLabel.text = $"{data.soldierHealth * healthMultiplier:0}";
            armySoldierAttackLabel.text = $"{data.soldierAttack * attackMultiplier:0}";
            armySoldierDefenseLabel.text = $"{data.soldierDefense * defenseMultiplier:0}";
        }
    }
}
