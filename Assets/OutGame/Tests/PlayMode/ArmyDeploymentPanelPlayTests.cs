using System.Collections;
using System.Linq;
using NUnit.Framework;
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
    /// </summary>
    public class ArmyDeploymentPanelPlayTests
    {
        private GameObject canvasGo;
        private GameObject eventSystemGo;
        private ArmyDeploymentPanel panel;
        private RunState run;
        private ArmyDefinition armyDef;
        private ItemDefinition bowDef;
        private ItemDefinition saddleDef;

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
            saddleDef = Resources.Load<ItemDefinition>("OutGame/Data/ItemDefinition_Saddle");
            Assert.IsNotNull(armyDef);
            Assert.IsNotNull(bowDef);
            Assert.IsNotNull(saddleDef);

            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 3, startingArmyDefId = "army_basic" });
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
                new[] { armyDef }, new[] { bowDef, saddleDef });
        }

        // OnItemDroppedOnCard는 처리를 한 프레임 늦추므로(코드 리뷰 CRITICAL 수정 — 드래그 종료 처리와의
        // 경합 방지), 테스트에서도 반드시 private 메서드를 호출한 뒤 프레임을 흘려보내야 결과가 반영된다.
        private void DropItemOnCard(ArmyCardView card, string itemId)
        {
            typeof(ArmyDeploymentPanel)
                .GetMethod("OnItemDroppedOnCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(panel, new object[] { card, itemId });
        }

        [UnityTest]
        public IEnumerator Open_SpawnsCardForEveryOwnedArmy()
        {
            OpenPanel();
            yield return null;

            var cards = panel.GetComponentsInChildren<ArmyCardView>(includeInactive: true);
            Assert.AreEqual(run.armies.Count, cards.Length);
        }

        [UnityTest]
        public IEnumerator Open_StartBattleButtonDisabledWhenEmpty()
        {
            OpenPanel();
            yield return null;

            Button startButton = panel.transform.Find("MainRow/CenterColumn/StartBattleButton").GetComponent<Button>();
            Assert.IsFalse(startButton.interactable, "빈 배치로는 전투 시작 불가 (§5.7)");
        }

        [UnityTest]
        public IEnumerator Confirmed_FiresWithDeployedArmy()
        {
            OpenPanel();
            yield return null;

            var slotView = panel.GetComponentsInChildren<DeploySlotView>().First();
            var cardView = panel.GetComponentsInChildren<ArmyCardView>(includeInactive: true).First();

            // 드래그 앤 드롭 시뮬레이션 없이, 배치 API를 직접 통해 계약을 검증
            // (uGUI 실제 드래그 시뮬레이션은 EventSystem raycast 의존이 커서 별도 통합 테스트로 분리)
            typeof(ArmyDeploymentPanel)
                .GetMethod("OnArmyDroppedOnSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(panel, new object[] { cardView.ArmyInstanceId, slotView.SlotId });
            yield return null;

            Logic.Battle.BattleSetupData received = null;
            panel.Confirmed += setup => received = setup;

            Button startButton = panel.transform.Find("MainRow/CenterColumn/StartBattleButton").GetComponent<Button>();
            Assert.IsTrue(startButton.interactable);
            startButton.onClick.Invoke();

            Assert.IsNotNull(received);
            Assert.AreEqual("room_2_0", received.roomId);
            Assert.AreEqual(1, received.armies.Count);
            Assert.AreEqual(cardView.ArmyInstanceId, received.armies[0].armyInstanceId);
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
        public IEnumerator ReturnToRoster_VacatesSlotAndUpdatesPower()
        {
            OpenPanel();
            yield return null;

            var slotView = panel.GetComponentsInChildren<DeploySlotView>().First();
            var cardView = panel.GetComponentsInChildren<ArmyCardView>(includeInactive: true).First();
            typeof(ArmyDeploymentPanel)
                .GetMethod("OnArmyDroppedOnSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(panel, new object[] { cardView.ArmyInstanceId, slotView.SlotId });
            yield return null;

            typeof(ArmyDeploymentPanel)
                .GetMethod("OnArmyReturned", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(panel, new object[] { cardView.ArmyInstanceId });
            yield return null;

            Text allyPower = panel.transform.Find("MainRow/AllyColumn/PowerLabel").GetComponent<Text>();
            Assert.AreEqual("전투력: 0", allyPower.text, "배치 해제 후 전투력이 0으로 복귀해야 함");
            Button startButton = panel.transform.Find("MainRow/CenterColumn/StartBattleButton").GetComponent<Button>();
            Assert.IsFalse(startButton.interactable);
        }

        [UnityTest]
        public IEnumerator ItemDrop_OnBasicArmy_EquipsAfterSuppressedConfirm()
        {
            // 억제 플래그를 미리 켜서(§5.6: 다시 표시 안 함) 팝업 없이 즉시 부여되는 경로를 검증
            PlayerPrefs.SetInt(ItemBindWarningPopup.SuppressPrefKey, 1);
            run.ownedItemIds.Add("item_bow");
            OpenPanel();
            yield return null;

            var cardView = panel.GetComponentsInChildren<ArmyCardView>(includeInactive: true).First();
            DropItemOnCard(cardView, "item_bow");
            yield return null; // HandleItemDropNextFrame의 1프레임 지연
            yield return null; // 부여 로직 완료 대기

            Assert.IsTrue(run.GetArmy(cardView.ArmyInstanceId).HasItem, "아이템이 부여됐어야 함");
            Assert.IsFalse(run.ownedItemIds.Contains("item_bow"), "귀속된 아이템은 보유 목록에서 제거");
        }

        [UnityTest]
        public IEnumerator ItemDrop_OnArmyAlreadyEquipped_IsIgnored()
        {
            PlayerPrefs.SetInt(ItemBindWarningPopup.SuppressPrefKey, 1);
            run.ownedItemIds.Add("item_bow");
            run.ownedItemIds.Add("item_saddle");
            OpenPanel();
            yield return null;

            var cardView = panel.GetComponentsInChildren<ArmyCardView>(includeInactive: true).First();
            DropItemOnCard(cardView, "item_bow");
            yield return null;
            yield return null;
            Assert.IsTrue(run.GetArmy(cardView.ArmyInstanceId).HasItem, "선행 조건: 활이 부여돼 있어야 함");

            // 이미 아이템이 귀속된 부대에 다른 아이템을 드롭 — 교체 불가이므로 무시돼야 함 (§4-6)
            DropItemOnCard(cardView, "item_saddle");
            yield return null;
            yield return null;

            Assert.AreEqual("item_bow", run.GetArmy(cardView.ArmyInstanceId).EquippedItemId, "귀속은 교체되면 안 됨");
            Assert.IsTrue(run.ownedItemIds.Contains("item_saddle"), "거부된 드롭은 보유 목록을 소모하면 안 됨");
        }

        [UnityTest]
        public IEnumerator ItemDrop_UnownedItem_IsIgnored()
        {
            OpenPanel();
            yield return null;

            var cardView = panel.GetComponentsInChildren<ArmyCardView>(includeInactive: true).First();
            DropItemOnCard(cardView, "item_saddle"); // ownedItemIds에 없음
            yield return null;
            yield return null;

            Assert.IsFalse(run.GetArmy(cardView.ArmyInstanceId).HasItem);
        }
    }
}
