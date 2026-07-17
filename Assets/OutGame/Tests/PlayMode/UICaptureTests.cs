using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// UI 스크린샷 캡처 — 시각 검증 루프용 (unity-ui-design 스킬의 핵심 부품 프로토타입).
    /// 캡처 결과: 프로젝트 루트 ui-captures/*.png → Claude가 이미지를 읽고 디자인을 판단·수정한다.
    /// batchmode(렌더 프레임 없음)에서는 자동 스킵.
    /// </summary>
    public class UICaptureTests
    {
        private static string CaptureDir
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ui-captures"));
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        [UnityTest]
        public IEnumerator Capture_RoomMapPanel_InitialAndMidRun()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("batchmode에서는 렌더 프레임이 없어 캡처 불가 — 에디터 열림 상태에서만 실행");
                yield break;
            }

            var canvasGo = new GameObject("CaptureCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject prefab = Resources.Load<GameObject>("OutGame/RoomMapPanel");
                Assert.IsNotNull(prefab, "RoomMapPanel 프리팹 없음 — SceneSetupM2.Run() 실행 필요");
                var panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<RoomMapPanel>();

                MapState map = new MapGenerator(new MapGenerationConfig(), seed: 42).Generate();
                panel.Open(map);

                yield return CaptureToFile("RoomMapPanel_01_initial.png");

                // 2개 방 방문 후 상태 (현재/방문/선택가능/잠김 + 경로 강조 확인용)
                MapProgress.Visit(map, MapProgress.GetSelectableNodes(map)[0].point);
                MapProgress.Visit(map, MapProgress.GetSelectableNodes(map)[0].point);
                panel.Refresh();

                yield return CaptureToFile("RoomMapPanel_02_midrun.png");
            }
            finally
            {
                Object.Destroy(canvasGo);
            }
        }

        [UnityTest]
        public IEnumerator Capture_ArmyDeploymentPanel_EmptyAndWithInventoryOpen()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("batchmode에서는 렌더 프레임이 없어 캡처 불가 — 에디터 열림 상태에서만 실행");
                yield break;
            }

            var canvasGo = new GameObject("CaptureCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject prefab = Resources.Load<GameObject>("OutGame/ArmyDeploymentPanel");
                Assert.IsNotNull(prefab, "ArmyDeploymentPanel 프리팹 없음 — SceneSetupM3UI.Run() 실행 필요");
                var panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<ArmyDeploymentPanel>();

                var armyDef = Resources.Load<ArmyDefinition>("OutGame/Data/ArmyDefinition_Basic");
                var bowDef = Resources.Load<ItemDefinition>("OutGame/Data/ItemDefinition_Bow");

                MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
                RunState run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 3, startingArmyDefId = "army_basic" });
                run.ownedItemIds.Add("item_bow");

                panel.Open(run, "room_2_0", RoomType.NormalBattle, "enc_default", new[] { armyDef }, new[] { bowDef });

                yield return CaptureToFile("ArmyDeploymentPanel_01_initial.png");

                // 첫 부대를 첫 슬롯에 배치한 상태도 확인 (전투력 갱신, 카드 재배치 확인용)
                var slotView = panel.GetComponentsInChildren<DeploySlotView>().First();
                var cardView = panel.GetComponentsInChildren<ArmyCardView>(includeInactive: true).First();
                typeof(ArmyDeploymentPanel)
                    .GetMethod("OnArmyDroppedOnSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(panel, new object[] { cardView.ArmyInstanceId, slotView.SlotId });

                yield return CaptureToFile("ArmyDeploymentPanel_02_deployed.png");

                // 인벤토리 팝업 오픈 상태
                Transform centerColumn = panel.transform.Find("MainRow/CenterColumn");
                centerColumn.Find("ItemButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

                yield return CaptureToFile("ArmyDeploymentPanel_03_inventory_open.png");
            }
            finally
            {
                Object.Destroy(canvasGo);
                Object.Destroy(eventSystemGo);
            }
        }

        [UnityTest]
        public IEnumerator Capture_EventAndRestPanels()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("batchmode에서는 렌더 프레임이 없어 캡처 불가 — 에디터 열림 상태에서만 실행");
                yield break;
            }

            var canvasGo = new GameObject("CaptureCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
                RunState run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 2, startingArmyDefId = "army_basic" });

                // EventPanel — 초기 상태 + 선택 후 결과 상태
                var eventPrefab = Resources.Load<GameObject>("OutGame/EventPanel");
                var eventDef = Resources.Load<EventDefinition>("OutGame/Data/Events/EventDefinition_Deserters");
                var eventPanel = Object.Instantiate(eventPrefab, canvasGo.transform).GetComponent<EventPanel>();
                eventPanel.Open(eventDef, run);
                yield return CaptureToFile("EventPanel_01_initial.png");

                var choiceButton = eventPanel.GetComponentsInChildren<UnityEngine.UI.Button>()
                    .First(b => b.transform.parent.name == "ChoiceContainer");
                choiceButton.onClick.Invoke();
                yield return CaptureToFile("EventPanel_02_result.png");
                Object.Destroy(eventPanel.gameObject);
                yield return null;

                // RestPanel — 초기 상태 + 선택 후 결과 상태
                // 참고: EventPanel/RestPanel은 65% 반투명 딤 배경을 쓰는데, 격리된 캡처 테스트에서는
                // 그 뒤에 실제로 렌더링되는 게 없어 에디터 Game 뷰의 이전 프레임 잔상이 비쳐 보일 수 있다
                // (unity-screenshot-capture 스킬 참조). 실제 게임에서는 항상 불투명한 RoomMapPanel 위에
                // 뜨므로 발생하지 않는다 — 격리 캡처 환경의 한계일 뿐 컴포넌트 결함이 아님 (검증 완료).
                var restPrefab = Resources.Load<GameObject>("OutGame/RestPanel");
                var armyDef = Resources.Load<ArmyDefinition>("OutGame/Data/ArmyDefinition_Basic");
                var restPanel = Object.Instantiate(restPrefab, canvasGo.transform).GetComponent<RestPanel>();
                restPanel.Open(run, new System.Collections.Generic.Dictionary<string, ArmyDefinition> { ["army_basic"] = armyDef });
                yield return CaptureToFile("RestPanel_01_initial.png");

                var optionButton = restPanel.GetComponentsInChildren<UnityEngine.UI.Button>()
                    .First(b => b.transform.parent.name == "ArmyListContainer");
                optionButton.onClick.Invoke();
                yield return CaptureToFile("RestPanel_02_result.png");
            }
            finally
            {
                Object.Destroy(canvasGo);
            }
        }

        private static IEnumerator CaptureToFile(string fileName)
        {
            yield return new WaitForEndOfFrame(); // 렌더 완료 시점에 백버퍼 캡처

            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(CaptureDir, fileName), texture.EncodeToPNG());
                Debug.Log($"[UICapture] 저장: ui-captures/{fileName} ({texture.width}x{texture.height})");
            }
            finally
            {
                Object.Destroy(texture);
            }
        }
    }
}
