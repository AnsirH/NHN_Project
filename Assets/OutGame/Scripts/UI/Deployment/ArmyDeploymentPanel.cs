using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 군대 배치 UI (상세 기획 §5.7 — 핵심 산출물). 씬 독립 프리팹.
    /// 사용법: Open(...) → 배치 편집(드래그 앤 드롭) → [전투 시작] → OnConfirmed(BattleSetupData).
    ///
    /// 레이아웃(2026-07-18 개정 — Mini Warriors 참고 이미지 재확인 후 로스터 목록 제거):
    /// 군대 보유 상한 = 배치 슬롯 수(§4-7)이므로 보유 군대는 항상 전부 슬롯에 들어간다.
    /// Open() 시점에 슬롯 ID 오름차순으로 자동 배치되며, 별도 "보유 군대" 목록/드롭존은 없다.
    /// 드래그 앤 드롭은 슬롯 간 이동/스왑(진형 변경)만 지원 — DeploymentState.Place가 이미 처리.
    /// 우측 "적 진영"은 슬롯 그리드만 표시(적 구성은 RoomEncounterTable 미결 — 자리만 예약).
    /// 중앙 축: 적 버프 표시(1차 "없음") → [전투 시작] → [아이템] → [프리셋](비활성).
    /// </summary>
    public class ArmyDeploymentPanel : MonoBehaviour
    {
        [Header("구조 참조")]
        [SerializeField] private RectTransform allySlotContainer;
        [SerializeField] private RectTransform enemySlotContainer;
        [SerializeField] private Text allyPowerLabel;
        [SerializeField] private Text enemyPowerLabel;
        [SerializeField] private Text enemyBuffLabel;
        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button itemButton;
        [SerializeField] private Button presetButton; // §4-17: 배치만, 항상 비활성

        [Header("요소 프리팹")]
        [SerializeField] private ArmyCardView armyCardPrefab;
        [SerializeField] private DeploySlotView allySlotPrefab;
        [SerializeField] private Image enemySlotPrefab;

        [Header("팝업")]
        [SerializeField] private ItemBindWarningPopup bindWarningPopup;
        [SerializeField] private InventoryPopup inventoryPopup;

        [Header("설정")]
        [SerializeField] private BattleFieldConfig fieldConfig;
        [SerializeField] private BattlePowerConfigAsset powerConfig;

        private readonly Dictionary<string, ArmyCardView> cardsByArmyId = new Dictionary<string, ArmyCardView>();
        private readonly Dictionary<string, ArmyDefinition> armyDefsById = new Dictionary<string, ArmyDefinition>();
        private readonly Dictionary<string, ItemDefinition> itemDefsById = new Dictionary<string, ItemDefinition>();
        private readonly Dictionary<int, DeploySlotView> allySlotViewsById = new Dictionary<int, DeploySlotView>();

        private RunState run;
        private DeploymentState deployment;
        private string roomId;
        private RoomType roomType;
        private string encounterId;

        public event Action<BattleSetupData> Confirmed;

        public void Open(
            RunState runState,
            string roomIdValue,
            RoomType roomTypeValue,
            string encounterIdValue,
            IReadOnlyList<ArmyDefinition> armyDefs,
            IReadOnlyList<ItemDefinition> itemDefs)
        {
            if (runState == null) throw new ArgumentNullException(nameof(runState));
            if (armyDefs == null) throw new ArgumentNullException(nameof(armyDefs));
            if (itemDefs == null) throw new ArgumentNullException(nameof(itemDefs));
            ValidateWiring();

            run = runState;
            roomId = roomIdValue;
            roomType = roomTypeValue;
            encounterId = encounterIdValue;

            armyDefsById.Clear();
            foreach (ArmyDefinition def in armyDefs) armyDefsById[def.ToData().id] = def;
            itemDefsById.Clear();
            foreach (ItemDefinition def in itemDefs) itemDefsById[def.ToData().id] = def;

            deployment = new DeploymentState(fieldConfig.ToData().GenerateSlots());
            if (run.armies.Count > deployment.SlotCount)
                throw new InvalidOperationException(
                    $"보유 군대({run.armies.Count})가 배치 슬롯 수({deployment.SlotCount})를 초과했습니다 — " +
                    "RunConfig.maxArmyCount와 BattleFieldConfig 슬롯 수가 어긋나 있습니다 (§4-7).");

            BuildSlots();
            BuildCards();
            AutoPlaceArmies();
            RefreshLayout();

            enemyBuffLabel.text = "없음"; // §4-21: 1차는 표시 영역만
            presetButton.interactable = false; // §4-17

            gameObject.SetActive(true);
            PanelTransitions.FadeIn(gameObject);
        }

        public void Close() => gameObject.SetActive(false);

        private void ValidateWiring()
        {
            if (allySlotContainer == null
                || enemySlotContainer == null || allyPowerLabel == null || enemyPowerLabel == null
                || enemyBuffLabel == null || startBattleButton == null || itemButton == null || presetButton == null)
                throw new InvalidOperationException("ArmyDeploymentPanel의 구조 참조가 배선되지 않았습니다.");
            if (armyCardPrefab == null || allySlotPrefab == null || enemySlotPrefab == null)
                throw new InvalidOperationException("ArmyDeploymentPanel의 요소 프리팹이 배선되지 않았습니다.");
            if (bindWarningPopup == null || inventoryPopup == null)
                throw new InvalidOperationException("ArmyDeploymentPanel의 팝업이 배선되지 않았습니다.");
            if (fieldConfig == null || powerConfig == null)
                throw new InvalidOperationException("ArmyDeploymentPanel의 config 에셋이 배선되지 않았습니다.");
        }

        private void Awake()
        {
            startBattleButton.onClick.AddListener(OnStartBattleClicked);
            itemButton.onClick.AddListener(OnItemButtonClicked);
        }

        private void OnDestroy()
        {
            startBattleButton.onClick.RemoveListener(OnStartBattleClicked);
            itemButton.onClick.RemoveListener(OnItemButtonClicked);
        }

        // ── 구성 ─────────────────────────────────────────────────────

        private void BuildSlots()
        {
            foreach (Transform child in allySlotContainer.Cast<Transform>().ToArray()) Destroy(child.gameObject);
            foreach (Transform child in enemySlotContainer.Cast<Transform>().ToArray()) Destroy(child.gameObject);
            allySlotViewsById.Clear();

            List<SlotDefinition> slots = fieldConfig.ToData().GenerateSlots();

            foreach (SlotDefinition slot in slots)
            {
                DeploySlotView view = Instantiate(allySlotPrefab, allySlotContainer);
                view.Initialize(slot.slotId);
                view.ArmyDropped += OnArmyDroppedOnSlot;
                allySlotViewsById[slot.slotId] = view;
            }

            // 적 진영 — 시각적 자리만 (RoomEncounterTable 미결, §9)
            for (int i = 0; i < slots.Count; i++)
                Instantiate(enemySlotPrefab, enemySlotContainer);
        }

        private void BuildCards()
        {
            foreach (ArmyCardView view in cardsByArmyId.Values)
                if (view != null) Destroy(view.gameObject);
            cardsByArmyId.Clear();

            // 임시 부모(패널 루트) — AutoPlaceArmies() 직후 RefreshLayout()이 슬롯 CardContainer로 재배치한다.
            foreach (ArmyInstance army in run.armies)
            {
                ArmyCardView card = Instantiate(armyCardPrefab, transform);
                card.Initialize(army.instanceId);
                card.DragEnded += OnCardDragEnded;
                card.ItemDropped += OnItemDroppedOnCard;
                cardsByArmyId[army.instanceId] = card;
            }
        }

        /// <summary>보유 군대 전원을 슬롯 ID 오름차순으로 자동 배치한다 (§4-7 — 상한=슬롯 수라 항상 전부 들어간다).</summary>
        private void AutoPlaceArmies()
        {
            List<int> slotIds = allySlotViewsById.Keys.OrderBy(id => id).ToList();
            for (int i = 0; i < run.armies.Count; i++)
                deployment.Place(run.armies[i].instanceId, slotIds[i]);
        }

        // ── 이벤트 처리 ──────────────────────────────────────────────
        //
        // uGUI 이벤트 순서: OnDrop(드롭 타깃) → OnEndDrag(드래그 소스), 같은 포인터-업 처리 안에서 순차 실행.
        // OnCardDragEnded(OnEndDrag 경유)가 항상 RefreshLayout을 호출하므로 실제 드래그 흐름에서는
        // 아래 호출이 중복이지만, Place/Remove 직후 상태를 즉시 반영해두면 드래그를 거치지 않는
        // 호출(테스트, 추후 클릭 배치 등)에서도 항상 일관된 화면을 보장한다.

        private void OnArmyDroppedOnSlot(string armyInstanceId, int slotId)
        {
            deployment.Place(armyInstanceId, slotId);
            RefreshLayout();
        }

        private void OnCardDragEnded(ArmyCardView view)
        {
            RefreshLayout();
        }

        private void OnItemDroppedOnCard(ArmyCardView card, string itemId)
        {
            if (bindWarningPopup.IsShowing) return; // 이미 확인 대기 중 — 새 드롭은 무시(카드는 원위치로 복귀)

            // 인벤토리 팝업 재구성(아이템 카드 파괴 포함)은 지금 진행 중인 드래그의 OnEndDrag보다
            // 절대 먼저 실행되면 안 된다 — 한 프레임 늦춰 안전하게 처리한다 (코드 리뷰 CRITICAL 수정).
            StartCoroutine(HandleItemDropNextFrame(card.ArmyInstanceId, itemId));
        }

        private IEnumerator HandleItemDropNextFrame(string armyInstanceId, string itemId)
        {
            yield return null;

            ArmyInstance army = run.GetArmy(armyInstanceId);
            if (army == null || !run.ownedItemIds.Contains(itemId) || army.HasItem)
                yield break; // 부여 불가 조건 — 조용히 무시 (드래그 실패와 동일하게 취급)

            string itemName = itemDefsById.TryGetValue(itemId, out ItemDefinition itemDef)
                ? itemDef.DisplayName
                : itemId;
            string armyName = armyDefsById.TryGetValue(army.armyDefId, out ArmyDefinition armyDef)
                ? armyDef.ToData().displayName
                : army.armyDefId;

            bindWarningPopup.ShowOrConfirmImmediately(itemName, armyName, () =>
            {
                ItemEquipService.Equip(run, army.instanceId, itemId);
                inventoryPopup.Rebuild(run.ownedItemIds, itemDefsById);
                RefreshLayout();
            });
        }

        private void OnItemButtonClicked() => inventoryPopup.Show(run.ownedItemIds, itemDefsById);

        private void OnStartBattleClicked()
        {
            if (!deployment.CanStartBattle) return;

            var armyDataById = armyDefsById.ToDictionary(kv => kv.Key, kv => kv.Value.ToData());
            var itemDataById = ToDataDict(itemDefsById);

            BattleSetupData setup = deployment.BuildSetup(roomId, roomType, encounterId, run, itemDataById, armyDataById);
            Confirmed?.Invoke(setup);
        }

        // ── 레이아웃 갱신 ────────────────────────────────────────────

        private void RefreshLayout()
        {
            Dictionary<string, ItemData> itemDataById = ToDataDict(itemDefsById);
            Dictionary<string, ArmyData> armyDataById = armyDefsById.ToDictionary(kv => kv.Key, kv => kv.Value.ToData());

            foreach (KeyValuePair<string, ArmyCardView> kv in cardsByArmyId)
            {
                ArmyInstance army = run.GetArmy(kv.Key);
                if (army == null) continue; // 부대가 런에서 사라진 경우(미래 기능 대비) — 카드만 남기고 스킵

                int? slotId = deployment.GetSlotOf(kv.Key);
                if (!slotId.HasValue || !allySlotViewsById.TryGetValue(slotId.Value, out DeploySlotView slotView))
                    throw new InvalidOperationException(
                        $"부대 {kv.Key}가 어떤 슬롯에도 배치되지 않았습니다 — 자동 배치 로직 확인 필요 (§4-7: 상한=슬롯 수).");
                Transform target = slotView.CardContainer;

                if (kv.Value.transform.parent != target)
                    kv.Value.transform.SetParent(target, worldPositionStays: false);

                ArmyClass armyClass = ItemEquipService.ResolveClass(army, itemDataById);
                string classLabel = armyClass == ArmyClass.None ? "" : armyClass.ToString();
                armyDefsById.TryGetValue(army.armyDefId, out ArmyDefinition def);
                Sprite portrait = def != null ? def.Portrait : null;
                string displayName = def != null ? def.ToData().displayName : army.armyDefId;
                kv.Value.SetDisplay(displayName, classLabel, portrait);
            }

            startBattleButton.interactable = deployment.CanStartBattle;
            UpdatePowerLabels(armyDataById, itemDataById);
        }

        private void UpdatePowerLabels(
            IReadOnlyDictionary<string, ArmyData> armyDataById, IReadOnlyDictionary<string, ItemData> itemDataById)
        {
            BattlePowerConfig power = powerConfig.ToData();

            float allyPower = 0f;
            foreach (KeyValuePair<string, ArmyCardView> kv in cardsByArmyId)
            {
                if (!deployment.GetSlotOf(kv.Key).HasValue) continue;

                ArmyInstance army = run.GetArmy(kv.Key);
                if (army == null || !armyDataById.TryGetValue(army.armyDefId, out ArmyData def)) continue;

                ArmyClass armyClass = ItemEquipService.ResolveClass(army, itemDataById);
                allyPower += (def.baseSoldierCount + army.bonusSoldierCount) * power.WeightOf(armyClass) + def.generalPower;
            }

            allyPowerLabel.text = $"전투력: {allyPower:0}";
            enemyPowerLabel.text = "전투력: -"; // RoomEncounterTable 미결 (§9)
        }

        private static Dictionary<string, ItemData> ToDataDict(Dictionary<string, ItemDefinition> defs) =>
            defs.ToDictionary(kv => kv.Key, kv => kv.Value.ToData());
    }
}
