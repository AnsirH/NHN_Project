using System.Collections;
using System.Linq;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// OutGameFlowController(§3.1 씬 통합의 최상위 코디네이터) 스모크 테스트 — 맵 선택→캐릭터 선택→
    /// 방 그래프 3패널 토글과 "이어하기"(RunSessionContext.PendingRun) 분기를 검증한다. 이전엔 이
    /// 코디네이터를 직접 검증하는 테스트가 없었다(코드 리뷰 HIGH 지적). OutGame.unity(빌드 세팅
    /// 등록됨)를 직접 로드해 실제 배선을 통합 검증한다.
    /// </summary>
    public class OutGameFlowControllerPlayTests
    {
        // Single 모드로 로드하면 테스트 러너의 기본 씬을 통째로 대체해 이후 다른 테스트에 잔상을
        // 남긴다 — Additive로 얹었다가 TearDown에서 반드시 걷어낸다(InGameFlowControllerBossFlowPlayTests와 동일 패턴).
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RunSessionContext.ConsumePendingRun(); // 정적 상태 정리 — 테스트 간 PendingRun 오염 방지
            BattleBridge.ResetToDefault();
            yield return SceneManager.UnloadSceneAsync(SceneNames.OutGame);
        }

        private static IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.OutGame, LoadSceneMode.Additive);
            yield return null;
        }

        private static MapSelectPanel MapSelect() =>
            Object.FindFirstObjectByType<MapSelectPanel>(FindObjectsInactive.Include);

        private static CharacterSelectPanel CharacterSelect() =>
            Object.FindFirstObjectByType<CharacterSelectPanel>(FindObjectsInactive.Include);

        private static InGameFlowController RoomGraph() =>
            Object.FindFirstObjectByType<InGameFlowController>(FindObjectsInactive.Include);

        private static LoreFragmentOverlayPanel LoreOverlay() =>
            Object.FindFirstObjectByType<LoreFragmentOverlayPanel>(FindObjectsInactive.Include);


        private static readonly string[] MapPointNames =
            { "MapPoint_1_SmallCastle", "MapPoint_2_CentralCastle", "MapPoint_3_Fortress" };

        private static Button ActiveMapPointButton(MapSelectPanel mapSelect) =>
            MapPointNames
                .Select(name => mapSelect.transform.Find(name).GetComponent<Button>())
                .First(b => b.interactable);

        private static RunState NewRun()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            var run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 1 });
            run.selectedCharacterId = Resources.LoadAll<OutGame.ScriptableObjects.PlayerCharacterDefinition>("OutGame/Data/Characters")[0].ToData().id;
            return run;
        }

        [UnityTest]
        public IEnumerator Load_NoPendingRun_ShowsOnlyMapSelectPanel()
        {
            RunSessionContext.ConsumePendingRun();
            yield return LoadScene();

            Assert.IsTrue(MapSelect().gameObject.activeSelf, "새 게임 진입은 맵 선택부터 시작해야 함");
            Assert.IsFalse(CharacterSelect().gameObject.activeSelf);
            Assert.IsFalse(RoomGraph().gameObject.activeSelf);
        }

        // 2026-10-19 부활: 전투 복귀(pendingRun)는 이제 "의지의 파편" 오버레이를 거친다. 파편이 걸리면
        // 오버레이가 먼저 뜨고 클릭해야 방 그래프로, 걸리지 않으면 오버레이 없이 곧장 방 그래프로 간다.
        // 결정성: 씬 로드로 패널이 생긴 뒤·컨트롤러 Start() 전에 FragmentTriggerChance를 0/1로 고정한다
        // (additive 비동기 로드 완료 시점엔 Awake만 돌고 Start는 다음 프레임에 돌기 때문).

        [UnityTest]
        public IEnumerator Load_WithPendingRun_FragmentTriggered_ShowsOverlayBeforeRoomGraph()
        {
            RunSessionContext.SetPendingRun(NewRun());
            yield return SceneManager.LoadSceneAsync(SceneNames.OutGame, LoadSceneMode.Additive);
            LoreOverlay().FragmentTriggerChance = 1f; // 파편 항상 뜨게 고정
            yield return null; // 이제 Start()가 돌며 loreOverlay.Begin(...) 실행

            Assert.IsTrue(LoreOverlay().gameObject.activeSelf, "파편이 뜨면 방 그래프보다 먼저 오버레이가 떠야 함");
            Assert.IsFalse(RoomGraph().gameObject.activeSelf, "클릭 전엔 방 그래프가 보이면 안 됨");
            Assert.IsNull(RunSessionContext.PendingRun, "PendingRun은 Start()에서 가장 먼저 소비돼야 함");
        }

        [UnityTest]
        public IEnumerator Load_WithPendingRun_NoFragment_SkipsOverlayStraightToRoomGraph()
        {
            RunSessionContext.SetPendingRun(NewRun());
            yield return SceneManager.LoadSceneAsync(SceneNames.OutGame, LoadSceneMode.Additive);
            LoreOverlay().FragmentTriggerChance = 0f; // 파편 절대 안 뜨게 고정
            yield return null;

            Assert.IsTrue(RoomGraph().gameObject.activeSelf, "파편이 없으면 오버레이 없이 방 그래프로 직행해야 함");
            Assert.IsFalse(LoreOverlay().gameObject.activeSelf, "파편이 없으면 오버레이는 뜨지 않아야 함");
            Assert.IsFalse(MapSelect().gameObject.activeSelf);
            Assert.IsFalse(CharacterSelect().gameObject.activeSelf);
            Assert.IsNull(RunSessionContext.PendingRun);
        }

        [UnityTest]
        public IEnumerator Load_WithPendingRun_FragmentContinueClick_ShowsOnlyRoomGraph()
        {
            RunSessionContext.SetPendingRun(NewRun());
            yield return SceneManager.LoadSceneAsync(SceneNames.OutGame, LoadSceneMode.Additive);
            LoreOverlay().FragmentTriggerChance = 1f;
            yield return null;

            PrefabBinding.Get<Button>(LoreOverlay(), "continueButton").onClick.Invoke();
            yield return null;

            Assert.IsFalse(LoreOverlay().gameObject.activeSelf, "클릭 후 오버레이는 닫혀야 함");
            Assert.IsTrue(RoomGraph().gameObject.activeSelf, "클릭 후 방 그래프로 진입해야 함");
            Assert.IsFalse(MapSelect().gameObject.activeSelf);
            Assert.IsFalse(CharacterSelect().gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator MapConfirmed_SwitchesToCharacterSelectPanelOnly()
        {
            yield return LoadScene();

            MapSelectPanel mapSelect = MapSelect();
            Button entryButton = ActiveMapPointButton(mapSelect);
            entryButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(mapSelect.gameObject.activeSelf);
            Assert.IsTrue(CharacterSelect().gameObject.activeSelf);
            Assert.IsFalse(RoomGraph().gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator CharacterConfirmed_SwitchesToRoomGraphOnly()
        {
            yield return LoadScene();

            ActiveMapPointButton(MapSelect()).onClick.Invoke();
            yield return null;

            CharacterSelectPanel characterSelect = CharacterSelect();
            PrefabBinding.Get<Button>(characterSelect, "confirmButton").onClick.Invoke();
            yield return null;

            Assert.IsFalse(MapSelect().gameObject.activeSelf);
            Assert.IsFalse(characterSelect.gameObject.activeSelf);
            Assert.IsTrue(RoomGraph().gameObject.activeSelf,
                "캐릭터 확정 후에는 방 그래프로 전환돼야 함");
        }
    }
}
