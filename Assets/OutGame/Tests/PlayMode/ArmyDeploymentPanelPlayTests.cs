using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// ArmyDeploymentPanel 프리팹 스모크 테스트 — 프리팹은 SceneSetupM3UI.Run()으로 생성돼 있어야 한다.
    /// 군대 보유 상한 = 배치 슬롯 수(§4-7)이므로 Open() 시점에 보유 군대 전원이 자동 배치된다.
    /// </summary>
    public class ArmyDeploymentPanelPlayTests
    {
        private GameObject canvasGo;
        private GameObject eventSystemGo;
        private ArmyDeploymentPanel panel;
        private RunState run;
        private ArmyDefinition armyDef;
        private ItemDefinition bowDef;
        private ItemDefinition shieldDef;
        private RunConfig runConfig;
        private static readonly List<EnemyArmy> TestEnemyComposition = new List<EnemyArmy>
        {
            new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.Archer, soldierCount = 30 },
            new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.Warrior, soldierCount = 30 },
            new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.None, soldierCount = 30 },
        };

        [SetUp]
        public void SetUp()
        {
            // PlayerPrefs는 세션 간 디스크에 남는다 — 이전 실행/수동 Play에서 억제 플래그가
            // 켜져 있으면 팝업 관련 테스트가 그 상태에 따라 다르게 동작한다 (테스트 오염 방지).
            PlayerPrefs.DeleteKey(ItemBindWarningPopup.SuppressPrefKey);

            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            GameObject prefab = Resources.Load<GameObject>("OutGame/ArmyDeploymentPanel");
            Assert.IsNotNull(prefab, "ArmyDeploymentPanel 프리팹이 없음 — SceneSetupM3UI.Run() 실행 필요");
            panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<ArmyDeploymentPanel>();

            armyDef = Resources.Load<ArmyDefinition>("OutGame/Data/ArmyDefinition_Basic");
            bowDef = Resources.Load<ItemDefinition>("OutGame/Data/ItemDefinition_Bow");
            shieldDef = Resources.Load<ItemDefinition>("OutGame/Data/ItemDefinition_Shield");
            Assert.IsNotNull(armyDef);
            Assert.IsNotNull(bowDef);
            Assert.IsNotNull(shieldDef);

            runConfig = new RunConfig { startingArmyCount = 3, startingArmyDefId = "army_basic" };
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            run = RunStateFactory.Create(map, runConfig);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
            Object.Destroy(eventSystemGo);
            PlayerPrefs.DeleteKey(ItemBindWarningPopup.SuppressPrefKey);
        }

        private void OpenPanel()
        {
            panel.Open(run, "room_2_0", RoomType.NormalBattle, "enc_default",
                new[] { armyDef }, new[] { bowDef, shieldDef }, runConfig, new AugmentDefinition[0],
                TestEnemyComposition);
        }

        // 플레이어 진영(슬롯/카드/드래그앤드롭/아이템장착/업그레이드)은 AllyFormationView가 담당한다
        // (2026-07-26 추출) — private 메서드 리플렉션 호출은 이제 이 컴포넌트를 대상으로 한다.
        private AllyFormationView AllyFormationView() =>
            panel.transform.Find("MainRow/AllyColumn").GetComponent<AllyFormationView>();

        // OnItemDroppedOnCard는 처리를 한 프레임 늦추므로(코드 리뷰 CRITICAL 수정 — 드래그 종료 처리와의
        // 경합 방지), 테스트에서도 반드시 private 메서드를 호출한 뒤 프레임을 흘려보내야 결과가 반영된다.
        private void DropItemOnCard(ArmyCardView card, string itemId)
        {
            typeof(AllyFormationView)
                .GetMethod("OnItemDroppedOnCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(AllyFormationView(), new object[] { card, itemId });
        }

        private void DropArmyOnSlot(string armyInstanceId, int slotId)
        {
            typeof(AllyFormationView)
                .GetMethod("OnArmyDroppedOnSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(AllyFormationView(), new object[] { armyInstanceId, slotId });
        }

        // 적 진영도 아군과 동일한 ArmyCardView를 재사용하므로(2026-07-26), panel 전체를 뒤지면 적
        // 카드까지 섞여 나온다 — 실제 보유 군대 카드만 필요한 테스트는 반드시 아군 격자로 범위를 좁힌다.
        private ArmyCardView[] AllyCards() =>
            panel.transform.Find("MainRow/AllyColumn/SlotGrid").GetComponentsInChildren<ArmyCardView>(includeInactive: true);

        // 자동 배치가 이제 slotId 오름차순이 아니라 "가운데 전방" 기준점에서부터 채워지므로
        // (2026-07-26 사용자 확정), slotId가 가장 낮은 슬롯이 더 이상 항상 점유돼 있다는 보장이 없다 —
        // 카드가 실제로 들어있는 슬롯만 걸러서 써야 한다.
        private List<DeploySlotView> OccupiedAllySlotsOrdered() =>
            panel.GetComponentsInChildren<DeploySlotView>()
                .Where(s => s.CardContainer.GetComponentInChildren<ArmyCardView>() != null)
                .OrderBy(s => s.SlotId)
                .ToList();

        [UnityTest]
        public IEnumerator RunConfigDefaultAsset_HasArmyUpgradeCosts()
        {
            // int[] 필드가 SceneSetupM3Data 재실행 후에도 에셋 YAML에 실제로 올바르게 저장/복원되는지
            // 확인한다(§4-26) — Unity가 primitive 배열을 hex 블롭으로 직렬화하는 것을 실제로 확인했음.
            var runConfigAsset = Resources.Load<RunConfigAsset>("OutGame/Data/RunConfig_Default");
            Assert.IsNotNull(runConfigAsset, "RunConfig_Default 에셋이 없음 — SceneSetupM3Data.Run() 실행 필요");
            CollectionAssert.AreEqual(new[] { 50, 100, 200, 350, 550 }, runConfigAsset.ToData().armyUpgradeCosts);
            yield break;
        }

        [UnityTest]
        public IEnumerator Open_SpawnsCardForEveryOwnedArmy()
        {
            OpenPanel();
            yield return null;

            var cards = AllyCards();
            Assert.AreEqual(run.armies.Count, cards.Length);
        }

        [UnityTest]
        public IEnumerator Open_AutoPlacesAllArmiesIntoSlots()
        {
            OpenPanel();
            yield return null;

            var cards = AllyCards();
            Assert.AreEqual(run.armies.Count, cards.Length);
            foreach (var card in cards)
                Assert.IsNotNull(card.GetComponentInParent<DeploySlotView>(), "자동 배치 후 모든 카드는 슬롯 안에 있어야 함 (§4-7)");

            Button startButton = panel.transform.Find("MainRow/CenterColumn/StartBattleButton").GetComponent<Button>();
            Assert.IsTrue(startButton.interactable, "전원 자동 배치되므로 Open() 직후 바로 전투 시작 가능해야 함");
        }

        [UnityTest]
        public IEnumerator Open_BuildsFullEnemyGridMatchingAllySize()
        {
            // 2026-07-19 사용자 요청: 적 진영도 플레이어 진영처럼 항상 꽉 찬 격자를 보여줘야 한다 —
            // 생성된 구성 개수(3개)만큼만 슬롯을 만들던 이전 방식은 폐기.
            OpenPanel();
            yield return null;

            Transform allySlotGrid = panel.transform.Find("MainRow/AllyColumn/SlotGrid");
            Transform enemySlotGrid = panel.transform.Find("MainRow/EnemyColumn/SlotGrid");
            Assert.IsNotNull(enemySlotGrid, "적 진영 슬롯 컨테이너가 있어야 함");
            Assert.AreEqual(allySlotGrid.childCount, enemySlotGrid.childCount,
                "적 진영 격자 크기는 아군과 같아야 함(§5.7)");
        }

        [UnityTest]
        public IEnumerator Open_EnemySlotsShowClassNamesOnlyForGeneratedUnits()
        {
            // 2026-07-26 사용자 요청: 적 진영도 아군과 동일한 ArmyCardView로 표시하되, 병사 수는
            // 어느 진영도 표시하지 않는다(뱃지/카운트 필드 자체를 제거) — 빈 슬롯은 배경만 있고
            // 카드 자체가 없어야 한다(구성 개수만큼만 카드 생성).
            OpenPanel();
            yield return null;

            Transform enemySlotGrid = panel.transform.Find("MainRow/EnemyColumn/SlotGrid");
            var enemyCards = enemySlotGrid.GetComponentsInChildren<ArmyCardView>(includeInactive: true);
            Assert.AreEqual(TestEnemyComposition.Count, enemyCards.Length, "생성된 적 구성 개수만큼만 카드가 있어야 함");

            var names = enemyCards.Select(card => card.transform.Find("NameLabel").GetComponent<Text>().text).ToList();
            CollectionAssert.AreEquivalent(new[] { "궁수", "전사", "기본" }, names);
        }

        [UnityTest]
        public IEnumerator Open_WarriorPlacedInFrontOfArcher()
        {
            // 2026-07-19 사용자 요청: 근접(전사)은 앞열, 원거리(궁수)는 뒷열.
            OpenPanel();
            yield return null;

            Transform enemySlotGrid = panel.transform.Find("MainRow/EnemyColumn/SlotGrid");
            var gridLayout = enemySlotGrid.GetComponent<GridLayoutGroup>();
            int columns = gridLayout.constraintCount;

            int ColumnOf(ArmyCardView card)
            {
                Transform slotRoot = card.transform.parent;
                while (slotRoot.parent != enemySlotGrid) slotRoot = slotRoot.parent;
                return slotRoot.GetSiblingIndex() % columns;
            }

            var enemyCards = enemySlotGrid.GetComponentsInChildren<ArmyCardView>(includeInactive: true);
            ArmyCardView warrior = enemyCards.First(c => c.transform.Find("NameLabel").GetComponent<Text>().text == "전사");
            ArmyCardView archer = enemyCards.First(c => c.transform.Find("NameLabel").GetComponent<Text>().text == "궁수");

            Assert.Less(ColumnOf(warrior), ColumnOf(archer),
                "전사 카드의 열 인덱스가 궁수보다 낮아야(더 앞열이어야) 함");
        }

        [UnityTest]
        public IEnumerator Open_ComputesRealEnemyPowerFromComposition()
        {
            // §4-28: 적도 아군과 동일한 전투력 공식(§4-22)으로 계산돼야 한다 — 더 이상 "-" 고정 아님.
            // army_basic 기준: (30×1.2+10) + (30×1.5+10) + (30×1.0+10) = 46 + 55 + 40 = 141
            OpenPanel();
            yield return null;

            Text enemyPowerLabel = panel.transform.Find("MainRow/EnemyColumn/PowerLabel").GetComponent<Text>();
            Assert.AreEqual("전투력: 141", enemyPowerLabel.text);
        }

        [Test]
        public void Open_WithNoArmies_StartBattleButtonDisabled()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            var emptyRun = new RunState { mapState = map };

            panel.Open(emptyRun, "room_2_0", RoomType.NormalBattle, "enc_default",
                new[] { armyDef }, new[] { bowDef, shieldDef }, runConfig, new AugmentDefinition[0],
                TestEnemyComposition);

            Button startButton = panel.transform.Find("MainRow/CenterColumn/StartBattleButton").GetComponent<Button>();
            Assert.IsFalse(startButton.interactable, "군대가 하나도 없으면 전투 시작 불가 (§5.7)");
        }

        [Test]
        public void Open_ArmyCountExceedsSlotCount_Throws()
        {
            // RunConfig 자체 검증(startingArmyCount<=maxArmyCount)은 통과시키되, 실제 배치판(4×7=28)보다
            // 많은 군대를 보유하게 만들어 config 불일치(§4-7) 방어 로직을 검증한다.
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            RunState overCapRun = RunStateFactory.Create(map,
                new RunConfig { startingArmyCount = 30, maxArmyCount = 30, startingArmyDefId = "army_basic" });

            Assert.Throws<System.InvalidOperationException>(() =>
                panel.Open(overCapRun, "room_2_0", RoomType.NormalBattle, "enc_default",
                    new[] { armyDef }, new[] { bowDef, shieldDef }, runConfig, new AugmentDefinition[0],
                    TestEnemyComposition));
        }

        [UnityTest]
        public IEnumerator Confirmed_FiresWithDeployedArmy()
        {
            OpenPanel();
            yield return null;

            Logic.Battle.BattleSetupData received = null;
            panel.Confirmed += setup => received = setup;

            Button startButton = panel.transform.Find("MainRow/CenterColumn/StartBattleButton").GetComponent<Button>();
            Assert.IsTrue(startButton.interactable, "자동 배치되므로 Open() 직후 바로 전투 시작 가능해야 함");
            startButton.onClick.Invoke();

            Assert.IsNotNull(received);
            Assert.AreEqual("room_2_0", received.roomId);
            Assert.AreEqual(run.armies.Count, received.armies.Count, "보유 군대 전원이 자동 배치되어 전투에 참여해야 함");
        }

        [UnityTest]
        public IEnumerator Close_WhenPopupsAreOpen_HidesBothPopups()
        {
            // 2026-07-26 사용자 요청: 전투 시작(→ InGameFlowController가 Close() 호출) 시 두 팝업
            // 모두 닫힌 상태가 되어야 한다.
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));
            panel.transform.Find("MainRow/CenterColumn/ItemButton").GetComponent<Button>().onClick.Invoke();

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            var inventoryPopup = panel.GetComponentInChildren<InventoryPopup>(includeInactive: true);
            Assert.IsTrue(infoPopup.gameObject.activeSelf, "선행 조건: 군대 정보 팝업이 열려 있어야 함");
            Assert.IsTrue(inventoryPopup.gameObject.activeSelf, "선행 조건: 인벤토리 팝업이 열려 있어야 함");

            panel.Close();

            Assert.IsFalse(infoPopup.gameObject.activeSelf, "Close() 후 군대 정보 팝업은 닫혀 있어야 함");
            Assert.IsFalse(inventoryPopup.gameObject.activeSelf, "Close() 후 인벤토리 팝업은 닫혀 있어야 함");
        }

        [UnityTest]
        public IEnumerator Open_WithPopupsLeftActiveFromPreviousSession_ForcesThemClosed()
        {
            // Close() 경로를 놓치는 다른 케이스가 있더라도, 다음 방에서 Open()이 다시 호출될 때는
            // 무조건 팝업이 닫힌 상태로 시작해야 한다(2026-07-26 사용자 요청 — 방어적 이중 처리).
            OpenPanel();
            yield return null;

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            var inventoryPopup = panel.GetComponentInChildren<InventoryPopup>(includeInactive: true);
            infoPopup.gameObject.SetActive(true);
            inventoryPopup.gameObject.SetActive(true);

            OpenPanel();
            yield return null;

            Assert.IsFalse(infoPopup.gameObject.activeSelf, "재오픈 시 이전에 남아있던 군대 정보 팝업은 강제로 닫혀야 함");
            Assert.IsFalse(inventoryPopup.gameObject.activeSelf, "재오픈 시 이전에 남아있던 인벤토리 팝업은 강제로 닫혀야 함");
        }

        [UnityTest]
        public IEnumerator SwapArmiesBetweenSlots_ReassignsBothCorrectly()
        {
            OpenPanel();
            yield return null;

            var slots = OccupiedAllySlotsOrdered();
            var firstCard = slots[0].CardContainer.GetComponentInChildren<ArmyCardView>();
            var secondCard = slots[1].CardContainer.GetComponentInChildren<ArmyCardView>();
            Assert.IsNotNull(firstCard);
            Assert.IsNotNull(secondCard);

            // 첫 슬롯의 카드를 두 번째 슬롯 위로 드래그 — DeploymentState.Place가 스왑을 처리 (§5.7)
            DropArmyOnSlot(firstCard.ArmyInstanceId, slots[1].SlotId);
            yield return null;

            Assert.AreSame(firstCard, slots[1].CardContainer.GetComponentInChildren<ArmyCardView>());
            Assert.AreSame(secondCard, slots[0].CardContainer.GetComponentInChildren<ArmyCardView>());
        }

        [UnityTest]
        public IEnumerator SwapArmiesBetweenSlots_ResetsAnchoredPositionToZero()
        {
            // 드래그 위치 버그(§5.7 2026-07-19) 회귀 테스트 — 재부모화 후 좌표가 리셋되지 않으면
            // 카드가 캔버스 밖으로 튕겨나간다.
            OpenPanel();
            yield return null;

            var slots = OccupiedAllySlotsOrdered();
            var firstCard = slots[0].CardContainer.GetComponentInChildren<ArmyCardView>();

            DropArmyOnSlot(firstCard.ArmyInstanceId, slots[1].SlotId);
            yield return null;

            Assert.AreEqual(Vector2.zero, ((RectTransform)firstCard.transform).anchoredPosition,
                "재부모화 후 카드는 슬롯 중앙(0,0)에 있어야 함");
        }

        [UnityTest]
        public IEnumerator Open_WithSameRun_RestoresPreviousDeployment()
        {
            // 배치 영속화(§5.7 2026-07-19) 검증 — 방을 넘어가도(Close→Open) 직접 옮긴 진형이 유지돼야 한다.
            OpenPanel();
            yield return null;

            var slots = OccupiedAllySlotsOrdered();
            string firstArmyId = slots[0].CardContainer.GetComponentInChildren<ArmyCardView>().ArmyInstanceId;
            string secondArmyId = slots[1].CardContainer.GetComponentInChildren<ArmyCardView>().ArmyInstanceId;

            DropArmyOnSlot(firstArmyId, slots[1].SlotId);
            yield return null;

            panel.Close();
            OpenPanel(); // 같은 run으로 재오픈 — 매번 자동 배치가 아니라 이전 배치를 복원해야 함
            yield return null;

            var reopenedSlots = OccupiedAllySlotsOrdered();
            Assert.AreEqual(secondArmyId, reopenedSlots[0].CardContainer.GetComponentInChildren<ArmyCardView>().ArmyInstanceId);
            Assert.AreEqual(firstArmyId, reopenedSlots[1].CardContainer.GetComponentInChildren<ArmyCardView>().ArmyInstanceId);
        }

        [UnityTest]
        public IEnumerator Open_WithStaleSlotIdInSavedDeployment_FallsBackToAutoPlace()
        {
            // BattleFieldConfig가 이 세이브 이후 더 작게 바뀌는 등, 저장된 slotId가 현재 배치판에
            // 더 이상 존재하지 않는 경우를 시뮬레이션 (§4-7 방어 로직 회귀 테스트 — 크래시 대신
            // 자동 배치로 대체돼야 함).
            run.deployment.Add(new ArmySlotAssignment { armyInstanceId = run.armies[0].instanceId, slotId = 9999 });

            Assert.DoesNotThrow(() => OpenPanel());
            yield return null;

            var cards = AllyCards();
            foreach (var card in cards)
                Assert.IsNotNull(card.GetComponentInParent<DeploySlotView>(), "유효하지 않은 저장 슬롯도 자동 배치로 대체돼야 함");
        }

        [UnityTest]
        public IEnumerator Open_WithNewlyAcquiredArmy_AutoPlacesIntoFreeSlot()
        {
            // 저장된 배치에 없는 새 부대(예: 이벤트로 획득 후 재오픈)는 남는 슬롯에 자동 배치돼야 한다 (§5.7).
            OpenPanel();
            yield return null;
            panel.Close();

            var newArmy = new ArmyInstance { instanceId = "army_new_extra", armyDefId = "army_basic" };
            run.armies.Add(newArmy);

            OpenPanel();
            yield return null;

            Assert.IsTrue(run.deployment.Any(a => a.armyInstanceId == newArmy.instanceId),
                "새로 얻은 부대는 남는 슬롯에 자동 배치되어 run.deployment에 반영돼야 함");
            var cards = AllyCards();
            Assert.IsTrue(cards.Any(c => c.ArmyInstanceId == newArmy.instanceId));
        }

        [UnityTest]
        public IEnumerator Open_BuildsPresetButtonDisabled()
        {
            OpenPanel();
            yield return null;

            Button presetButton = panel.transform.Find("MainRow/CenterColumn/PresetButton").GetComponent<Button>();
            Assert.IsFalse(presetButton.interactable, "프리셋은 §4-17에 따라 항상 비활성");
        }

        [UnityTest]
        public IEnumerator ItemDrop_OnBasicArmy_EquipsAfterSuppressedConfirm()
        {
            // 억제 플래그를 미리 켜서(§5.6: 다시 표시 안 함) 팝업 없이 즉시 부여되는 경로를 검증
            PlayerPrefs.SetInt(ItemBindWarningPopup.SuppressPrefKey, 1);
            run.ownedItemIds.Add("item_bow");
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            DropItemOnCard(cardView, "item_bow");
            yield return null; // HandleItemDropNextFrame의 1프레임 지연
            yield return null; // 부여 로직 완료 대기

            Assert.IsTrue(run.GetArmy(cardView.ArmyInstanceId).HasItem, "아이템이 부여됐어야 함");
            Assert.IsFalse(run.ownedItemIds.Contains("item_bow"), "귀속된 아이템은 보유 목록에서 제거");

            // 병과가 생기면 이름 자체가 바뀌어야 한다 (§2 용어: 기본 군대 + 활 = 궁수 군대). 이름이 이미
            // 병과를 나타내므로 별도 뱃지는 아예 존재하지 않는다(2026-07-26 사용자 확정 — 카운트/뱃지
            // 필드 완전 제거).
            Text nameLabel = cardView.transform.Find("NameLabel").GetComponent<Text>();
            Assert.AreEqual("궁수 군대", nameLabel.text);
            Assert.IsNull(cardView.transform.Find("ClassBadge"), "ClassBadge 필드는 완전히 제거돼야 함");
        }

        [UnityTest]
        public IEnumerator Open_InventoryPopupHasNoDimBackground()
        {
            // 인벤토리 팝업은 모달이 아니다 — 아이템을 팝업 밖 부대 카드로 드래그해서 부여해야 하므로
            // dim 자체가 없어야 한다(2026-07-19 사용자 피드백 — raycastTarget=false만으로는 배경이
            // 여전히 어둡게 보였음. Image 컴포넌트 자체를 제거).
            OpenPanel();
            yield return null;

            var inventoryPopup = panel.GetComponentInChildren<InventoryPopup>(includeInactive: true);
            Assert.IsNull(inventoryPopup.GetComponent<Image>(), "인벤토리 팝업 루트에는 dim Image가 없어야 함");
        }

        [UnityTest]
        public IEnumerator ItemDrop_WithoutSuppression_ShowsWarningPopupAboveInventoryPopup()
        {
            // 인벤토리 팝업이 열려 있는 상태에서 귀속 확인 팝업이 그보다 뒤(먼저 자식) 순서면 가려서 안 보인다
            // (2026-07-19 버그 수정 회귀 테스트).
            run.ownedItemIds.Add("item_bow");
            OpenPanel();
            yield return null;

            var inventoryPopup = panel.GetComponentInChildren<InventoryPopup>(includeInactive: true);
            var bindWarningPopup = panel.GetComponentInChildren<ItemBindWarningPopup>(includeInactive: true);

            panel.transform.Find("MainRow/CenterColumn/ItemButton").GetComponent<Button>().onClick.Invoke();
            var cardView = AllyCards().First();
            DropItemOnCard(cardView, "item_bow");
            yield return null;
            yield return null;

            Assert.IsTrue(bindWarningPopup.IsShowing, "억제 플래그가 꺼져 있으면 확인 팝업이 떠야 함");
            Assert.Greater(bindWarningPopup.transform.GetSiblingIndex(), inventoryPopup.transform.GetSiblingIndex(),
                "경고 팝업은 항상 인벤토리 팝업보다 위(나중 형제)에 있어야 함");
        }

        [UnityTest]
        public IEnumerator InventoryPopup_OpenedAfterArmyInfoPopup_AppearsOnTop()
        {
            // 2026-07-26 사용자 요청: 마지막에 연 팝업이 항상 가장 위에 보여야 한다. ArmyInfoPopup은
            // 이미 SetAsLastSibling을 호출하므로, 그 뒤에 인벤토리 팝업을 열면 인벤토리가 더 위로
            // 올라와야 한다(반대 순서로 열렸을 때도 성립해야 하는 대칭 케이스).
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            var inventoryPopup = panel.GetComponentInChildren<InventoryPopup>(includeInactive: true);
            Assert.IsTrue(infoPopup.gameObject.activeSelf, "선행 조건: 군대 정보 팝업이 먼저 열려 있어야 함");

            panel.transform.Find("MainRow/CenterColumn/ItemButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.Greater(inventoryPopup.transform.GetSiblingIndex(), infoPopup.transform.GetSiblingIndex(),
                "나중에 연 인벤토리 팝업이 군대 정보 팝업보다 위(나중 형제)에 있어야 함");
        }

        [UnityTest]
        public IEnumerator ItemDrop_OnArmyAlreadyEquipped_IsIgnored()
        {
            PlayerPrefs.SetInt(ItemBindWarningPopup.SuppressPrefKey, 1);
            run.ownedItemIds.Add("item_bow");
            run.ownedItemIds.Add("item_shield");
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            DropItemOnCard(cardView, "item_bow");
            yield return null;
            yield return null;
            Assert.IsTrue(run.GetArmy(cardView.ArmyInstanceId).HasItem, "선행 조건: 활이 부여돼 있어야 함");

            // 이미 아이템이 귀속된 부대에 다른 아이템을 드롭 — 교체 불가이므로 무시돼야 함 (§4-6)
            DropItemOnCard(cardView, "item_shield");
            yield return null;
            yield return null;

            Assert.AreEqual("item_bow", run.GetArmy(cardView.ArmyInstanceId).EquippedItemId, "귀속은 교체되면 안 됨");
            Assert.IsTrue(run.ownedItemIds.Contains("item_shield"), "거부된 드롭은 보유 목록을 소모하면 안 됨");
        }

        [UnityTest]
        public IEnumerator ItemDrop_OnSlotMarginAroundCard_EquipsSameAsDirectCardDrop()
        {
            // 4×7 확장(cellSize 190x130 > 카드 140x88, §5.7) 이후 카드 주위에 빈 여백이 생겨, 그 여백에
            // 드롭하면 DeploySlotView가 이벤트를 받되 처리 안 하고 무시하던 회귀 버그 — 카드로 위임돼야 함.
            PlayerPrefs.SetInt(ItemBindWarningPopup.SuppressPrefKey, 1);
            run.ownedItemIds.Add("item_bow");
            OpenPanel();
            yield return null;

            var slots = OccupiedAllySlotsOrdered();
            var occupantCard = slots[0].CardContainer.GetComponentInChildren<ArmyCardView>();

            panel.transform.Find("MainRow/CenterColumn/ItemButton").GetComponent<Button>().onClick.Invoke();
            var itemCard = panel.GetComponentsInChildren<ItemCardView>(includeInactive: true).First();

            // 카드가 아니라 슬롯 자체(여백 포함 전체 영역)에 드롭된 상황을 재현 — DeploySlotView.OnDrop 직접 호출.
            var eventData = new PointerEventData(eventSystemGo.GetComponent<EventSystem>()) { pointerDrag = itemCard.gameObject };
            slots[0].OnDrop(eventData);
            yield return null; // HandleItemDropNextFrame의 1프레임 지연
            yield return null;

            Assert.IsTrue(run.GetArmy(occupantCard.ArmyInstanceId).HasItem, "슬롯 여백에 드롭해도 카드에 드롭한 것과 동일하게 장착돼야 함");
        }

        [UnityTest]
        public IEnumerator ItemDrop_UnownedItem_IsIgnored()
        {
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            DropItemOnCard(cardView, "item_shield"); // ownedItemIds에 없음
            yield return null;
            yield return null;

            Assert.IsFalse(run.GetArmy(cardView.ArmyInstanceId).HasItem);
        }

        // ── 군대 정보 팝업 (§5.7) ────────────────────────────────────

        [UnityTest]
        public IEnumerator Click_OnArmyCard_OpensArmyInfoPopupWithResolvedData()
        {
            // 아이템 부여 후 클릭하면 배치 UI와 동일하게 병과 반영 이름("궁수 군대")이 나와야 한다 —
            // 각자 계산하지 않고 ItemEquipService.ResolveDisplayName을 재사용해야 함.
            PlayerPrefs.SetInt(ItemBindWarningPopup.SuppressPrefKey, 1);
            run.ownedItemIds.Add("item_bow");
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            DropItemOnCard(cardView, "item_bow");
            yield return null;
            yield return null;

            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Assert.IsTrue(infoPopup.gameObject.activeSelf, "카드 클릭 시 군대 정보 팝업이 열려야 함");

            Text armyNameLabel = infoPopup.transform.Find("Window/BodyRow/ArmyColumn/ArmyInfo/ArmyNameLabel").GetComponent<Text>();
            Assert.AreEqual("궁수 군대", armyNameLabel.text);
        }

        [UnityTest]
        public IEnumerator Click_OnArmyCard_ShowsGeneralStatGridIconValues()
        {
            // 군대 정보.png(2026-07-19): 장군 스탯은 상단 체력·공격력·방어력 / 하단 치명타·이동속도
            // 아이콘+수치 그리드로 표시돼야 한다(텍스트 한 줄 "전투력 보정: X" 아님).
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            const string gridPath = "Window/BodyRow/GeneralColumn/GeneralStatsGrid/";
            Assert.AreEqual("100", infoPopup.transform.Find(gridPath + "TopRow/Health/Value").GetComponent<Text>().text);
            Assert.AreEqual("10", infoPopup.transform.Find(gridPath + "TopRow/Attack/Value").GetComponent<Text>().text);
            Assert.AreEqual("5", infoPopup.transform.Find(gridPath + "TopRow/Defense/Value").GetComponent<Text>().text);
            Assert.AreEqual("5%", infoPopup.transform.Find(gridPath + "BottomRow/CritRate/Value").GetComponent<Text>().text);
            Assert.AreEqual("100", infoPopup.transform.Find(gridPath + "BottomRow/MoveSpeed/Value").GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator Click_OnArmyCard_ShowsArmyStatGridWithoutCritOrSpeed()
        {
            // 병사는 치명타·이동속도를 장군에게서 물려받으므로 군대 쪽 그리드엔 체력·공격력·방어력 3개만
            // 있어야 한다(§5.7 참고 이미지).
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            const string gridPath = "Window/BodyRow/ArmyColumn/ArmyStatsGrid/Row/";
            Assert.AreEqual("50", infoPopup.transform.Find(gridPath + "Health/Value").GetComponent<Text>().text);
            Assert.AreEqual("5", infoPopup.transform.Find(gridPath + "Attack/Value").GetComponent<Text>().text);
            Assert.AreEqual("2", infoPopup.transform.Find(gridPath + "Defense/Value").GetComponent<Text>().text);
            Assert.IsNull(infoPopup.transform.Find(gridPath + "CritRate"), "군대 스탯 그리드엔 치명타 칸이 없어야 함");
            Assert.IsNull(infoPopup.transform.Find(gridPath + "MoveSpeed"), "군대 스탯 그리드엔 이동속도 칸이 없어야 함");
        }

        [UnityTest]
        public IEnumerator Click_OnArmyCard_ShowsGeneralPreviewIcon()
        {
            // 2026-07-19 사용자 피드백: 병사 프리뷰와 대응되는 장군 프리뷰 아이콘 — 정사각형이어야 한다.
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            RectTransform generalPreviewIcon = (RectTransform)infoPopup.transform.Find(
                "Window/BodyRow/GeneralColumn/GeneralPreview/Icon");
            Assert.IsNotNull(generalPreviewIcon, "장군 프리뷰 아이콘이 있어야 함");
            Assert.AreEqual(generalPreviewIcon.sizeDelta.x, generalPreviewIcon.sizeDelta.y, "장군 프리뷰 아이콘은 정사각형이어야 함");
        }

        [UnityTest]
        public IEnumerator Click_OnArmyCard_ShowsGeneralNameUnderPreviewIcon()
        {
            // 2026-07-19 사용자 요청: 병사 프리뷰(아이콘+병사 수)와 마찬가지로 장군 프리뷰 아이콘
            // 아래에도 장군 이름을 표시한다.
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Text previewNameLabel = infoPopup.transform
                .Find("Window/BodyRow/GeneralColumn/GeneralPreview/GeneralPreviewNameLabel").GetComponent<Text>();
            Assert.AreEqual("이름 없는 장군", previewNameLabel.text);
        }

        [UnityTest]
        public IEnumerator Click_OnArmyCard_StatIconsAreSquare()
        {
            // 2026-07-19 사용자 피드백: 스탯 아이콘은 정사각형이어야 함.
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            RectTransform generalIcon = (RectTransform)infoPopup.transform.Find(
                "Window/BodyRow/GeneralColumn/GeneralStatsGrid/TopRow/Health/Icon");
            RectTransform armyIcon = (RectTransform)infoPopup.transform.Find(
                "Window/BodyRow/ArmyColumn/ArmyStatsGrid/Row/Health/Icon");
            Assert.AreEqual(generalIcon.sizeDelta.x, generalIcon.sizeDelta.y, "장군 스탯 아이콘은 정사각형이어야 함");
            Assert.AreEqual(armyIcon.sizeDelta.x, armyIcon.sizeDelta.y, "군대 스탯 아이콘은 정사각형이어야 함");
        }

        [UnityTest]
        public IEnumerator Click_OnArmyCard_ShowsSoldierCountUnderPreviewIcon()
        {
            // 2026-07-19 사용자 피드백: army meta 패널(병과 텍스트) 삭제, 대신 병사 프리뷰 아이콘 아래
            // 병사 수를 표시한다 — 참고 이미지의 "Lv 30/50" 자리.
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Assert.IsNull(infoPopup.transform.Find("Window/BodyRow/ArmyColumn/ArmyMeta"), "army meta 패널은 삭제돼야 함");

            Text soldierCountLabel = infoPopup.transform
                .Find("Window/BodyRow/ArmyColumn/SoldierPreview/SoldierCountLabel").GetComponent<Text>();
            Assert.AreEqual($"30/{armyDef.ToData().maxSoldierCount}명", soldierCountLabel.text);
        }

        [UnityTest]
        public IEnumerator Click_OnArmyCard_ShowsArmyDescription()
        {
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Text descriptionLabel = infoPopup.transform.Find("Window/BodyRow/ArmyColumn/ArmyDescription/Label").GetComponent<Text>();
            Assert.AreEqual("-", descriptionLabel.text, "설명이 비어 있으면 플레이스홀더 '-'를 보여줘야 함");
        }

        [UnityTest]
        public IEnumerator Click_OnArmyCard_ShowsUpgradeLevelZeroInitially()
        {
            // 장군 경험치는 제거되고 군대 업그레이드(§4-26)로 대체됐다(2026-07-19).
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Text upgradeLevelLabel = infoPopup.transform
                .Find("Window/BodyRow/GeneralColumn/UpgradeBand/UpgradeLevelLabel").GetComponent<Text>();
            Assert.AreEqual("+0", upgradeLevelLabel.text);
        }

        [UnityTest]
        public IEnumerator Click_OnUpgradeButton_WithEnoughGold_IncrementsLevelAndDeductsGold()
        {
            run.gold = 1000;
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            int costForFirstLevel = runConfig.armyUpgradeCosts[0];
            infoPopup.transform.Find("Window/BodyRow/GeneralColumn/UpgradeBand/UpgradeButton")
                .GetComponent<Button>().onClick.Invoke();

            Text upgradeLevelLabel = infoPopup.transform
                .Find("Window/BodyRow/GeneralColumn/UpgradeBand/UpgradeLevelLabel").GetComponent<Text>();
            Text currencyLabel = infoPopup.transform.Find("Window/CurrencyLabel").GetComponent<Text>();
            Assert.AreEqual("+1", upgradeLevelLabel.text);
            Assert.AreEqual($"재화: {1000 - costForFirstLevel}", currencyLabel.text);
        }

        [UnityTest]
        public IEnumerator UpgradeButton_WithInsufficientGold_IsNotInteractable()
        {
            run.gold = 0;
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Button upgradeButton = infoPopup.transform
                .Find("Window/BodyRow/GeneralColumn/UpgradeBand/UpgradeButton").GetComponent<Button>();
            Assert.IsFalse(upgradeButton.interactable, "재화 부족 시 업그레이드 버튼은 비활성이어야 함");
        }

        [UnityTest]
        public IEnumerator ArmyInfoPopup_UpgradeButton_ShowsNextLevelCost()
        {
            // 2026-07-26 사용자 요청: 버튼에 다음 단계 비용이 보여야 함.
            run.gold = 1000;
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Text upgradeButtonLabel = infoPopup.transform
                .Find("Window/BodyRow/GeneralColumn/UpgradeBand/UpgradeButton/Label").GetComponent<Text>();
            Assert.AreEqual($"업그레이드 ({runConfig.armyUpgradeCosts[0]})", upgradeButtonLabel.text);
        }

        [UnityTest]
        public IEnumerator ArmyInfoPopup_UpgradeButton_ShowsMaxAtMaxLevel()
        {
            run.gold = 100000;
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Button upgradeButton = infoPopup.transform
                .Find("Window/BodyRow/GeneralColumn/UpgradeBand/UpgradeButton").GetComponent<Button>();
            Text upgradeButtonLabel = upgradeButton.transform.Find("Label").GetComponent<Text>();

            for (int i = 0; i < ArmyInstance.MaxUpgradeLevel; i++)
                upgradeButton.onClick.Invoke();

            Assert.AreEqual("MAX", upgradeButtonLabel.text, "최대 단계에서는 비용 대신 MAX를 표시해야 함");
            Assert.IsFalse(upgradeButton.interactable);
        }

        [UnityTest]
        public IEnumerator ArmyInfoPopup_HasNoDimBackground()
        {
            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Assert.IsNull(infoPopup.GetComponent<Image>(), "군대 정보 팝업 루트에는 dim Image가 없어야 함");
            yield break;
        }

        [UnityTest]
        public IEnumerator ArmyInfoPopup_CloseButton_HidesPopup()
        {
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Assert.IsTrue(infoPopup.gameObject.activeSelf);

            infoPopup.transform.Find("Window/CloseButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsFalse(infoPopup.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator ArmyInfoPopup_ShowsCurrentGold()
        {
            run.gold = 42;
            OpenPanel();
            yield return null;

            var cardView = AllyCards().First();
            cardView.OnPointerClick(new PointerEventData(eventSystemGo.GetComponent<EventSystem>()));

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Text currencyLabel = infoPopup.transform.Find("Window/CurrencyLabel").GetComponent<Text>();
            Assert.AreEqual("재화: 42", currencyLabel.text);
        }
    }
}
