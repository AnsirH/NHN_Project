using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Battle;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 플레이어 진영(슬롯 그리드/카드/드래그 앤 드롭/아이템 장착/업그레이드/전투력) 전용 뷰
    /// (2026-07-26, ArmyDeploymentPanel에서 추출) — 배치 화면의 "플레이어 진영" 컬럼과 방 그래프의
    /// "진영" 팝업(ArmyFormationPopup)이 이 스크립트(컴포넌트 클래스)를 공유한다. 단, 런타임엔 각
    /// 호스트 프리팹 안에 서로 독립된 인스턴스가 하나씩 있다(하나의 살아있는 오브젝트를 공유하는 게
    /// 아니다) — 두 인스턴스가 어긋나지 않는 이유는 배치/보유 군대/아이템 등 모든 상태를 항상
    /// RunState에서 다시 읽어 그리기 때문(§ Open()). 각자 따로 구현하면 배치 로직이 두 벌로 갈라져
    /// 어긋날 위험이 있어 공용 컴포넌트로 분리(사용자 확정) — 인스턴스 캐시(armyDefsById 등)는
    /// 인스턴스별로 독립이라는 점에 주의.
    /// 자기 GameObject의 활성 상태는 관리하지 않는다 — 호스트(ArmyDeploymentPanel/ArmyFormationPopup)가
    /// 화면 전체를 열고 닫는 시점에 맞춰 관리한다.
    /// </summary>
    public class AllyFormationView : MonoBehaviour
    {
        [Header("구조 참조")]
        [SerializeField] private RectTransform allySlotContainer;
        [SerializeField] private Text allyPowerLabel;
        [SerializeField] private Button itemButton;

        [Header("요소 프리팹")]
        [SerializeField] private ArmyCardView armyCardPrefab;
        [SerializeField] private DeploySlotView allySlotPrefab;

        [Header("팝업")]
        [SerializeField] private ItemBindWarningPopup bindWarningPopup;
        [SerializeField] private InventoryPopup inventoryPopup;
        [SerializeField] private ArmyInfoPopup armyInfoPopup;

        [Header("설정")]
        [SerializeField] private BattleFieldConfig fieldConfig;
        [SerializeField] private BattlePowerConfigAsset powerConfig;

        private readonly Dictionary<string, ArmyCardView> cardsByArmyId = new Dictionary<string, ArmyCardView>();
        private readonly Dictionary<string, ArmyDefinition> armyDefsById = new Dictionary<string, ArmyDefinition>();
        private readonly Dictionary<string, ItemDefinition> itemDefsById = new Dictionary<string, ItemDefinition>();
        private readonly Dictionary<string, AugmentDefinition> augmentDefsById = new Dictionary<string, AugmentDefinition>();
        private readonly Dictionary<int, DeploySlotView> allySlotViewsById = new Dictionary<int, DeploySlotView>();
        private readonly List<int> allySlotPriorityOrder = new List<int>();

        private RunState run;
        private RunConfig runConfig;
        private DeploymentState deployment;
        private bool selectionModeEnabled;

        /// <summary>배치/아이템 장착/업그레이드로 상태가 바뀔 때마다 RefreshLayout 끝에서 발행 —
        /// 호스트가 자신의 표시(전투력 대비 시작 버튼, 재화 표시 등)를 함께 갱신하는 데 쓴다.</summary>
        public event Action Changed;

        /// <summary>selectionMode가 true일 때 카드를 클릭하면 정보 팝업 대신 이 이벤트가 발행된다
        /// (armyInstanceId 전달) — 예: 증원 방에서 증원 대상을 고를 때(2026-07-26).</summary>
        public event Action<string> ArmySelected;

        public DeploymentState Deployment => deployment;

        public void Open(
            RunState runState,
            RunConfig runConfigValue,
            IReadOnlyList<ArmyDefinition> armyDefs,
            IReadOnlyList<ItemDefinition> itemDefs,
            IReadOnlyList<AugmentDefinition> augmentDefs,
            bool selectionMode = false)
        {
            if (runState == null) throw new ArgumentNullException(nameof(runState));
            if (runConfigValue == null) throw new ArgumentNullException(nameof(runConfigValue));
            if (armyDefs == null) throw new ArgumentNullException(nameof(armyDefs));
            if (itemDefs == null) throw new ArgumentNullException(nameof(itemDefs));
            if (augmentDefs == null) throw new ArgumentNullException(nameof(augmentDefs));
            ValidateWiring();

            run = runState;
            runConfig = runConfigValue;
            selectionModeEnabled = selectionMode;

            armyDefsById.Clear();
            foreach (ArmyDefinition def in armyDefs) armyDefsById[def.ToData().id] = def;
            itemDefsById.Clear();
            foreach (ItemDefinition def in itemDefs) itemDefsById[def.ToData().id] = def;
            augmentDefsById.Clear();
            foreach (AugmentDefinition def in augmentDefs) augmentDefsById[def.ToData().id] = def;

            deployment = new DeploymentState(fieldConfig.ToData().GenerateSlots());
            if (run.armies.Count > deployment.SlotCount)
                throw new InvalidOperationException(
                    $"보유 군대({run.armies.Count})가 배치 슬롯 수({deployment.SlotCount})를 초과했습니다 — " +
                    "RunConfig.maxArmyCount와 BattleFieldConfig 슬롯 수가 어긋나 있습니다 (§4-7).");

            BuildAllySlots();
            BuildCards();
            RestoreOrAutoPlaceArmies();
            RefreshLayout();

            // 이전 세션에서 팝업을 열어둔 채로 이 뷰를 다시 열었을 가능성에 대비한 방어적 초기화
            // (2026-07-26) — Close()에서도 닫지만, 그 경로를 놓치는 경우까지 이중으로 막는다.
            inventoryPopup.Hide();
            armyInfoPopup.Hide();
            bindWarningPopup.Hide();
        }

        /// <summary>자기 팝업 3종을 닫는다 — 호스트가 화면을 닫을 때 호출.</summary>
        public void Close()
        {
            inventoryPopup.Hide();
            armyInfoPopup.Hide();
            bindWarningPopup.Hide();
        }

        private void ValidateWiring()
        {
            if (allySlotContainer == null || allyPowerLabel == null || itemButton == null)
                throw new InvalidOperationException("AllyFormationView의 구조 참조가 배선되지 않았습니다.");
            if (armyCardPrefab == null || allySlotPrefab == null)
                throw new InvalidOperationException("AllyFormationView의 요소 프리팹이 배선되지 않았습니다.");
            if (bindWarningPopup == null || inventoryPopup == null || armyInfoPopup == null)
                throw new InvalidOperationException("AllyFormationView의 팝업이 배선되지 않았습니다.");
            if (fieldConfig == null || powerConfig == null)
                throw new InvalidOperationException("AllyFormationView의 config 에셋이 배선되지 않았습니다.");
        }

        private void Awake()
        {
            itemButton.onClick.AddListener(OnItemButtonClicked);
            // 업그레이드는 팝업 안에서 골드를 차감하므로(ArmyUpgradeService), 전투력/재화 표시도
            // 같이 갱신해야 한다 — RefreshLayout이 이미 둘 다 갱신하므로 재사용한다.
            armyInfoPopup.Upgraded += RefreshLayout;
        }

        private void OnDestroy()
        {
            itemButton.onClick.RemoveListener(OnItemButtonClicked);
            armyInfoPopup.Upgraded -= RefreshLayout;
        }

        // ── 구성 ─────────────────────────────────────────────────────

        private void BuildAllySlots()
        {
            foreach (Transform child in allySlotContainer.Cast<Transform>().ToArray()) Destroy(child.gameObject);
            allySlotViewsById.Clear();

            BattleFieldConfigData fieldData = fieldConfig.ToData();
            List<SlotDefinition> slots = fieldData.GenerateSlots();

            foreach (SlotDefinition slot in slots)
            {
                DeploySlotView view = Instantiate(allySlotPrefab, allySlotContainer);
                view.Initialize(slot.slotId);
                view.ArmyDropped += OnArmyDroppedOnSlot;
                view.ItemDroppedOnOccupant += OnItemDroppedOnCard;
                allySlotViewsById[slot.slotId] = view;
            }

            // 자동 배치 우선순위 — "가운데 전방"이 기본 배치 기준점이다(2026-07-26 사용자 확정).
            // 아군은 구역 제한 없이 전방 열(마지막 열)부터 후방 쪽으로(4→3→2→1) 훑고, 각 열 안에서는
            // 가운데 행부터 위아래로 번갈아 채운다. RestoreOrAutoPlaceArmies()가 이 순서를 사용한다.
            List<int> columnPriority = Enumerable.Range(0, fieldData.columns).Reverse().ToList();
            allySlotPriorityOrder.Clear();
            allySlotPriorityOrder.AddRange(
                SlotPriorityOrder.ByColumnPriority(slots, fieldData.columns, columnPriority).Select(s => s.slotId));
        }

        private void BuildCards()
        {
            foreach (ArmyCardView view in cardsByArmyId.Values)
                if (view != null) Destroy(view.gameObject);
            cardsByArmyId.Clear();

            // 임시 부모(이 뷰 루트) — RestoreOrAutoPlaceArmies() 직후 RefreshLayout()이 슬롯 CardContainer로 재배치한다.
            foreach (ArmyInstance army in run.armies)
            {
                ArmyCardView card = Instantiate(armyCardPrefab, transform);
                card.Initialize(army.instanceId);
                card.DragEnded += OnCardDragEnded;
                card.ItemDropped += OnItemDroppedOnCard;
                card.Clicked += OnCardClicked;
                cardsByArmyId[army.instanceId] = card;
            }
        }

        /// <summary>
        /// run.deployment에 저장된 배치를 먼저 복원하고, 아직 슬롯이 없는 부대(최초 방문 시 전부 해당,
        /// 또는 방문 사이 이벤트로 새로 얻은 부대)만 남는 슬롯에 자동 배치한다 (§5.7 2026-07-19 개정
        /// — 배치는 방을 넘어가도 유지되어야 하며, 매번 새로 자동 배치하면 안 된다). 자동 배치 순서는
        /// allySlotPriorityOrder(가운데 전방 기준, 2026-07-26 사용자 확정)를 따른다.
        /// </summary>
        private void RestoreOrAutoPlaceArmies()
        {
            foreach (ArmySlotAssignment saved in run.deployment)
                // 방어적: 저장된 부대가 더 이상 없거나, BattleFieldConfig가 그 사이 바뀌어(§4-7) 저장된
                // slotId가 더 이상 존재하지 않으면 건너뜀 — 아래 자동 배치 루프가 남는 슬롯에 채워준다.
                if (run.GetArmy(saved.armyInstanceId) != null && deployment.IsValidSlot(saved.slotId))
                    deployment.Place(saved.armyInstanceId, saved.slotId);

            List<int> freeSlotIds = allySlotPriorityOrder
                .Where(id => deployment.GetArmyAt(id) == null)
                .ToList();

            int freeIndex = 0;
            foreach (ArmyInstance army in run.armies)
            {
                if (deployment.GetSlotOf(army.instanceId).HasValue) continue; // 이미 복원됨
                if (freeIndex >= freeSlotIds.Count)
                    throw new InvalidOperationException(
                        "배치 슬롯이 부족합니다 — RunConfig.maxArmyCount와 BattleFieldConfig 슬롯 수 확인 필요 (§4-7).");
                deployment.Place(army.instanceId, freeSlotIds[freeIndex]);
                freeIndex++;
            }
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

        private void OnCardClicked(ArmyCardView card)
        {
            if (selectionModeEnabled)
            {
                ArmySelected?.Invoke(card.ArmyInstanceId);
                return;
            }

            ArmyInstance army = run.GetArmy(card.ArmyInstanceId);
            if (army == null) return; // 방어적 — 카드는 항상 살아있는 부대에만 존재해야 함
            if (!armyDefsById.TryGetValue(army.armyDefId, out ArmyDefinition def))
            {
                Debug.LogWarning($"[AllyFormationView] armyDefId '{army.armyDefId}'에 대한 ArmyDefinition을 찾을 수 없어 정보 팝업을 열지 못했습니다.");
                return;
            }

            var augmentDataById = augmentDefsById.ToDictionary(kv => kv.Key, kv => kv.Value.ToData());
            armyInfoPopup.Open(army, def, ToDataDict(itemDefsById), run, runConfig, augmentDataById);
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
                {
                    kv.Value.transform.SetParent(target, worldPositionStays: false);
                    // worldPositionStays:false는 로컬 위치 값을 그대로 유지한다 — 드래그 중이던 카드는
                    // 그 값이 "루트 캔버스 기준 마우스 좌표"라 슬롯 컨테이너 스케일과 안 맞아 화면 밖으로
                    // 튕겨나간다. 명시적으로 리셋해야 슬롯 중앙에 정확히 들어온다 (버그 수정).
                    ((RectTransform)kv.Value.transform).anchoredPosition = Vector2.zero;
                }

                armyDefsById.TryGetValue(army.armyDefId, out ArmyDefinition def);
                Sprite portrait = def != null ? def.Portrait : null;
                string baseDisplayName = def != null ? def.ToData().displayName : army.armyDefId;
                string displayName = ItemEquipService.ResolveDisplayName(army, baseDisplayName, itemDataById);
                // 이름 자체가 병과를 나타내므로(§2 용어: 기본 군대 + 활 = 궁수 군대) 별도 뱃지/병사 수는
                // 카드에 표시하지 않는다(2026-07-26 사용자 확정 — 진영 카드엔 군대 수 표시 안 함).
                kv.Value.SetDisplay(displayName, portrait);
            }

            // 배치 영속화를 먼저 끝내둔다 — 전투력 계산은 BattlePowerCalculator를 거치며 데이터
            // 무결성 위반 시 예외를 던지므로(§4-28), 순서가 반대면 방금 한 Place()/Remove()가
            // run.deployment에 반영되지 못한 채로 예외가 날 수 있다.
            SyncDeploymentToRunState();

            List<DeployedArmy> allyDeployed = deployment.BuildDeployedArmies(run, itemDataById, armyDataById);
            float allyPower = BattlePowerCalculator.Calculate(allyDeployed, powerConfig.ToData(), armyDataById);
            allyPowerLabel.text = $"전투력: {allyPower:0}";

            Changed?.Invoke();
        }

        /// <summary>
        /// 현재 배치를 run.deployment에 다시 쓴다 — 방을 넘어가도 유지되어야 하므로(§5.7) 여기서 매번
        /// 동기화한다. Place() 이후엔 항상 RefreshLayout이 호출되므로 별도 훅이 필요 없다.
        /// </summary>
        private void SyncDeploymentToRunState()
        {
            run.deployment.Clear();
            foreach (KeyValuePair<string, int> placement in deployment.Placements)
                run.deployment.Add(new ArmySlotAssignment { armyInstanceId = placement.Key, slotId = placement.Value });
        }

        private static Dictionary<string, ItemData> ToDataDict(Dictionary<string, ItemDefinition> defs) =>
            defs.ToDictionary(kv => kv.Key, kv => kv.Value.ToData());
    }
}
