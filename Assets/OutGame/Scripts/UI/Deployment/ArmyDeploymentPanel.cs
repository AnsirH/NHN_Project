using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Battle;
using OutGame.Logic.Characters;
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
    /// 플레이어 진영(슬롯/카드/드래그앤드롭/아이템장착/업그레이드/전투력)은 AllyFormationView가
    /// 담당한다(2026-07-26 추출 — 방 그래프의 "진영" 팝업(ArmyFormationPopup)과 공유하기 위함).
    /// 이 클래스는 적 진영(슬롯/자동배치/전투력)과 중앙 버튼 축(전투 시작/프리셋), 상단 재화 표시만
    /// 담당한다.
    ///
    /// 우측 "적 진영"은 아군과 같은 크기의 격자를 항상 전부 표시하고, EnemyCompositionGenerator가
    /// 생성한 구성(병과+병사 수)을 EnemyFormationAssigner로 열(column) 배치한다(§4-28) — 근접
    /// 병과는 앞열, 원거리 병과는 뒷열에 군집(2026-07-19 사용자 요청). 유닛 UI는 아군과 동일한
    /// ArmyCardView를 재사용하되 interactable=false로 표시 전용 처리한다(2026-07-26 사용자 요청).
    /// 이 구성은 표시·전투력·드롭 계산뿐 아니라 2026-07-29부터 BuildSetup()을 통해 실제 전투 스폰
    /// 데이터(BattleSetupData.enemies)로도 그대로 전달된다(§9 RoomEncounterTable 참고).
    /// 중앙 축: 적 버프 표시(1차 "없음") → [전투 시작] → [아이템] → [프리셋](비활성).
    /// </summary>
    public class ArmyDeploymentPanel : MonoBehaviour
    {
        [Header("구조 참조")]
        [SerializeField] private RectTransform enemySlotContainer;
        [SerializeField] private Text enemyPowerLabel;
        [SerializeField] private Text enemyBuffLabel;
        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button presetButton; // §4-17: 배치만, 항상 비활성
        [SerializeField] private CurrencyDisplay currencyDisplay; // 2026-07-26: 상단 바 재화 표시

        [Header("요소 프리팹")]
        [SerializeField] private ArmyCardView armyCardPrefab; // 적 카드 표시용(interactable=false)
        [SerializeField] private Image enemySlotPrefab;

        [Header("플레이어 진영")]
        [SerializeField] private AllyFormationView allyFormationView;

        [Header("설정")]
        [SerializeField] private BattleFieldConfig fieldConfig;
        [SerializeField] private BattlePowerConfigAsset powerConfig;

        private readonly Dictionary<string, ArmyDefinition> armyDefsById = new Dictionary<string, ArmyDefinition>();
        private readonly Dictionary<string, ItemDefinition> itemDefsById = new Dictionary<string, ItemDefinition>();
        private readonly Dictionary<string, PlayerCharacterDefinition> characterDefsById = new Dictionary<string, PlayerCharacterDefinition>();
        // 위 *DefsById와 함께 Open()에서만 채워지는 변환 캐시 — UpdateEnemyPowerLabel()/OnStartBattleClicked()
        // 둘 다 같은 변환을 다시 만들 필요가 없다(코드 리뷰 MEDIUM 지적 반영).
        private readonly Dictionary<string, ArmyData> armyDataById = new Dictionary<string, ArmyData>();
        private readonly Dictionary<string, ItemData> itemDataById = new Dictionary<string, ItemData>();
        private readonly Dictionary<string, PlayerCharacterData> characterDataById = new Dictionary<string, PlayerCharacterData>();
        private readonly Dictionary<int, Transform> enemySlotCardContainersById = new Dictionary<int, Transform>();

        private RunState run;
        private string roomId;
        private RoomType roomType;
        private string encounterId;
        private IReadOnlyList<EnemyArmy> enemyComposition = Array.Empty<EnemyArmy>();

        public event Action<BattleSetupData> Confirmed;

        public void Open(
            RunState runState,
            string roomIdValue,
            RoomType roomTypeValue,
            string encounterIdValue,
            IReadOnlyList<ArmyDefinition> armyDefs,
            IReadOnlyList<ItemDefinition> itemDefs,
            RunConfig runConfigValue,
            IReadOnlyList<AugmentDefinition> augmentDefs,
            IReadOnlyList<PlayerCharacterDefinition> characterDefs,
            IReadOnlyList<EnemyArmy> enemyCompositionValue)
        {
            if (runState == null) throw new ArgumentNullException(nameof(runState));
            if (armyDefs == null) throw new ArgumentNullException(nameof(armyDefs));
            if (itemDefs == null) throw new ArgumentNullException(nameof(itemDefs));
            if (runConfigValue == null) throw new ArgumentNullException(nameof(runConfigValue));
            if (augmentDefs == null) throw new ArgumentNullException(nameof(augmentDefs));
            if (characterDefs == null) throw new ArgumentNullException(nameof(characterDefs));
            if (enemyCompositionValue == null) throw new ArgumentNullException(nameof(enemyCompositionValue));
            ValidateWiring();

            run = runState;
            roomId = roomIdValue;
            roomType = roomTypeValue;
            encounterId = encounterIdValue;
            enemyComposition = enemyCompositionValue;

            armyDefsById.Clear();
            armyDataById.Clear();
            foreach (ArmyDefinition def in armyDefs)
            {
                ArmyData data = def.ToData();
                armyDefsById[data.id] = def;
                armyDataById[data.id] = data;
            }
            itemDefsById.Clear();
            itemDataById.Clear();
            foreach (ItemDefinition def in itemDefs)
            {
                ItemData data = def.ToData();
                itemDefsById[data.id] = def;
                itemDataById[data.id] = data;
            }
            characterDefsById.Clear();
            characterDataById.Clear();
            foreach (PlayerCharacterDefinition def in characterDefs)
            {
                PlayerCharacterData data = def.ToData();
                characterDefsById[data.id] = def;
                characterDataById[data.id] = data;
            }

            BuildEnemySlots();
            UpdateEnemyPowerLabel();

            enemyBuffLabel.text = "없음"; // §4-21: 1차는 표시 영역만
            presetButton.interactable = false; // §4-17

            allyFormationView.Open(run, runConfigValue, armyDefs, itemDefs, augmentDefs);
            // AllyFormationView.Changed 구독(Awake)에만 기대지 않고 최초 1회는 직접 동기화한다 —
            // 이 패널의 GameObject가 비활성 상태에서 Open()이 먼저 호출되면(씬에서 항상 그렇다,
            // SceneSetupM6UI가 SetActive(false)로 배치) Unity가 Awake()를 활성화 시점까지 미뤄서,
            // 첫 Open() 때는 구독이 아직 안 걸려있을 수 있다(이번 세션에 이미 겪은 것과 동일한
            // Awake 타이밍 문제 — 업그레이드 버튼 라벨, CurrencyDisplay).
            SyncFromAllyFormation();

            gameObject.SetActive(true);
            PanelTransitions.FadeIn(gameObject);
        }

        /// <summary>
        /// 패널을 닫는다 — 전투 시작 확정 직후(InGameFlowController.OnBattleSetupConfirmed) 호출된다.
        /// 플레이어 진영 팝업들은 AllyFormationView의 자식이라 부모(이 패널)가 비활성화되면 화면에서는
        /// 같이 사라지지만 각자의 activeSelf는 그대로 남아, 다음 방에서 패널이 다시 열릴 때 그대로
        /// 재노출될 수 있다(2026-07-26 사용자 지적) — AllyFormationView.Close()로 명시적으로 닫는다.
        /// </summary>
        public void Close()
        {
            allyFormationView.Close();
            gameObject.SetActive(false);
        }

        private void ValidateWiring()
        {
            if (enemySlotContainer == null || enemyPowerLabel == null
                || enemyBuffLabel == null || startBattleButton == null || presetButton == null
                || currencyDisplay == null)
                throw new InvalidOperationException("ArmyDeploymentPanel의 구조 참조가 배선되지 않았습니다.");
            if (armyCardPrefab == null || enemySlotPrefab == null)
                throw new InvalidOperationException("ArmyDeploymentPanel의 요소 프리팹이 배선되지 않았습니다.");
            if (allyFormationView == null)
                throw new InvalidOperationException("ArmyDeploymentPanel의 allyFormationView가 배선되지 않았습니다.");
            if (fieldConfig == null || powerConfig == null)
                throw new InvalidOperationException("ArmyDeploymentPanel의 config 에셋이 배선되지 않았습니다.");
        }

        private void Awake()
        {
            startBattleButton.onClick.AddListener(OnStartBattleClicked);
            allyFormationView.Changed += SyncFromAllyFormation;
        }

        private void OnDestroy()
        {
            startBattleButton.onClick.RemoveListener(OnStartBattleClicked);
            allyFormationView.Changed -= SyncFromAllyFormation;
        }

        private void SyncFromAllyFormation()
        {
            startBattleButton.interactable = allyFormationView.Deployment.CanStartBattle;
            currencyDisplay.SetAmount(run.gold);
        }

        // ── 구성 ─────────────────────────────────────────────────────

        private void BuildEnemySlots()
        {
            foreach (Transform child in enemySlotContainer.Cast<Transform>().ToArray()) Destroy(child.gameObject);
            enemySlotCardContainersById.Clear();

            BattleFieldConfigData fieldData = fieldConfig.ToData();
            List<SlotDefinition> slots = fieldData.GenerateSlots();

            // 적 진영 — 아군과 똑같은 크기의 격자를 항상 전부 만들고(§5.7 "플레이어 진영처럼"), 그 위에
            // §4-28 생성된 구성을 EnemyFormationAssigner로 배치한다. 유닛 표시는 아군과 동일한
            // ArmyCardView를 재사용한다(2026-07-26 사용자 요청 — 진영 간 UI 통일). 이 구성은 여기서는
            // 표시용으로만 쓰이지만, BuildSetup() 시점엔 동일한 enemyComposition이 그대로 §7 계약에
            // 실려 나간다 — 화면에 보이는 것과 실제 전투에 스폰되는 것이 항상 같다.
            foreach (SlotDefinition slot in slots)
            {
                Image slotBackground = Instantiate(enemySlotPrefab, enemySlotContainer);
                Transform cardContainer = slotBackground.transform.Find("CardContainer");
                if (cardContainer == null)
                {
                    Debug.LogWarning("[ArmyDeploymentPanel] EnemySlotPlaceholder 프리팹에 CardContainer가 없습니다 — SceneSetupM3UI.Run() 재실행 필요.");
                    continue;
                }
                enemySlotCardContainersById[slot.slotId] = cardContainer;
            }

            var assignments = EnemyFormationAssigner.Assign(enemyComposition, slots, fieldData.columns);
            foreach ((EnemyArmy enemy, SlotDefinition slot) in assignments)
            {
                if (!enemySlotCardContainersById.TryGetValue(slot.slotId, out Transform container)) continue;

                ArmyCardView card = Instantiate(armyCardPrefab, container);
                // 적 유닛은 run.armies에 속하지 않는 임시 표시용이라 실제 armyInstanceId가 없다 —
                // interactable=false로 드래그/클릭/아이템 드롭을 막으므로 이 값은 식별용으로만 쓰인다.
                card.Initialize($"enemy_{slot.slotId}", interactable: false);
                armyDefsById.TryGetValue(enemy.armyDefId, out ArmyDefinition def);
                card.SetDisplay(EnemyClassLabel(enemy.armyClass), def != null ? def.Portrait : null);
                ((RectTransform)card.transform).anchoredPosition = Vector2.zero;
            }
        }

        private static string EnemyClassLabel(ArmyClass armyClass) =>
            armyClass == ArmyClass.None ? "기본" : ItemEquipService.ClassDisplayName(armyClass);

        private void OnStartBattleClicked()
        {
            if (!allyFormationView.Deployment.CanStartBattle) return;

            List<AugmentData> selectedAugments = allyFormationView.BuildSelectedAugments();

            BattleSetupData setup = allyFormationView.Deployment.BuildSetup(
                roomId, roomType, encounterId, run, itemDataById, armyDataById, selectedAugments, characterDataById,
                enemyComposition);
            Confirmed?.Invoke(setup);
        }

        /// <summary>
        /// 적 전투력은 적 구성이 확정되는 Open() 시점에 한 번만 계산한다 — 적 구성은 세션 중 바뀌지
        /// 않으므로(플레이어 배치 변경과 무관), 예전처럼 아군이 바뀔 때마다 다시 계산할 필요가 없다
        /// (2026-07-26 추출 겸 정리). 아군/적 전투력은 같은 계산기(BattlePowerCalculator, §4-22)를
        /// 쓴다 — 예전엔 두 벌로 각자 계산해서 값이 어긋날 여지가 있었다(2026-07-19 통합).
        /// </summary>
        private void UpdateEnemyPowerLabel()
        {
            BattlePowerConfig power = powerConfig.ToData();

            // EnemyArmy는 DeployedArmy의 armyDefId/armyClass/soldierCount에만 대응 개념이 있다 —
            // 나머지(슬롯 위치 등)는 생성된 적에게 의미가 없어 기본값으로 두며, BattlePowerCalculator는
            // 그 필드들을 아예 읽지 않으므로 안전하다.
            List<DeployedArmy> enemyDeployed = enemyComposition.Select(enemy => new DeployedArmy
            {
                armyDefId = enemy.armyDefId,
                armyClass = enemy.armyClass,
                soldierCount = enemy.soldierCount,
            }).ToList();
            // 적은 업그레이드/증강 개념이 없으므로(§4-28) 빈 목록을 넘긴다 — DeployedArmy.upgradeLevel도
            // 기본값 0이라 배율은 항상 1.0으로 계산된다.
            float enemyPower = BattlePowerCalculator.Calculate(enemyDeployed, power, armyDataById, new List<AugmentData>());
            enemyPowerLabel.text = $"전투력: {enemyPower:0}";
        }
    }
}
