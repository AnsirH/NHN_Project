using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OutGame.Flow;
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

            yield return SettleGameView(); // 이전 테스트 클래스가 남긴 캔버스 잔상 제거

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

            yield return SettleGameView(); // 직전 테스트가 남긴 캔버스 잔상 제거

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

            yield return SettleGameView(); // 직전 테스트가 남긴 캔버스 잔상 제거

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
                eventPanel.Open(eventDef, run, maxArmyCountValue: 9);
                yield return CaptureToFile("EventPanel_01_initial.png");

                var choiceButton = eventPanel.GetComponentsInChildren<UnityEngine.UI.Button>()
                    .First(b => b.transform.parent.name == "ChoiceContainer");
                choiceButton.onClick.Invoke();
                yield return CaptureToFile("EventPanel_02_result.png");
                Object.Destroy(eventPanel.gameObject);
                yield return null;

                // RestPanel — 초기 상태 + 선택 후 결과 상태
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

        [UnityTest]
        public IEnumerator Capture_MainMenuAndMapSelectScreens()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("batchmode에서는 렌더 프레임이 없어 캡처 불가 — 에디터 열림 상태에서만 실행");
                yield break;
            }

            yield return SettleGameView(); // 직전 테스트가 남긴 캔버스 잔상 제거

            var canvasGo = new GameObject("CaptureCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            string tempSavePath = Path.Combine(Path.GetTempPath(), $"capture_save_{System.Guid.NewGuid():N}.json");
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var mainMenuPrefab = Resources.Load<GameObject>("OutGame/MainMenuScreen");
                Assert.IsNotNull(mainMenuPrefab, "MainMenuScreen 프리팹 없음 — SceneSetupM5UI.Run() 실행 필요");
                var mainMenu = Object.Instantiate(mainMenuPrefab, canvasGo.transform).GetComponent<MainMenuController>();
                mainMenu.SetSavePath(tempSavePath); // 저장 없음 → 이어하기 비활성 상태
                yield return CaptureToFile("MainMenu_01_no_save.png");

                MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
                RunState run = RunStateFactory.Create(map, new RunConfig());
                RunSaveService.Save(run, tempSavePath);
                mainMenu.SetSavePath(tempSavePath); // 저장 있음 → 이어하기 활성 상태
                yield return CaptureToFile("MainMenu_02_with_save.png");

                Object.Destroy(mainMenu.gameObject);
                yield return null;

                var mapSelectPrefab = Resources.Load<GameObject>("OutGame/MapSelectScreen");
                Assert.IsNotNull(mapSelectPrefab, "MapSelectScreen 프리팹 없음 — SceneSetupM5UI.Run() 실행 필요");
                Object.Instantiate(mapSelectPrefab, canvasGo.transform);
                yield return null; // Start()에서 MapDefinition 목록을 스폰할 시간
                yield return CaptureToFile("MapSelect_01_initial.png");
            }
            finally
            {
                Object.Destroy(canvasGo);
                if (File.Exists(tempSavePath)) File.Delete(tempSavePath);
            }
        }

        /// <summary>
        /// 진단 결과(2026-07-19): 캡처가 "너무 빨라서"가 아니라 직전 캡처(또는 이전 테스트가 파괴한
        /// 캔버스)의 프레임을 그대로 반환하는 지연이 있었다 — Game 뷰 리페인트를 명시적으로 강제하고
        /// 여러 프레임을 흘려보내야 실제 최신 프레임을 가져온다. 새 캔버스 생성 전(이전 테스트 잔상 제거)과
        /// 캡처 직전(방금 만든 콘텐츠 반영) 양쪽에서 호출한다.
        /// </summary>
        private static IEnumerator SettleGameView()
        {
            for (int i = 0; i < 8; i++)
            {
                ForceRepaintGameView();
                yield return new WaitForSecondsRealtime(0.05f);
                yield return new WaitForEndOfFrame();
            }
        }

        private static IEnumerator CaptureToFile(string fileName)
        {
            yield return SettleGameView();

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

#if UNITY_EDITOR
        private static void ForceRepaintGameView()
        {
            var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            var window = UnityEditor.EditorWindow.GetWindow(gameViewType, false, null, false);
            window?.Repaint();
        }
#else
        private static void ForceRepaintGameView() { }
#endif
    }
}
