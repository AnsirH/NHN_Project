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
            LoadingOverlayPanel.ResetForTests(); // DontDestroyOnLoad 싱글톤 정리
            yield return SceneManager.UnloadSceneAsync(SceneNames.OutGame);
        }

        private static IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.OutGame, LoadSceneMode.Additive);
            yield return null;
        }

        private static MapSelectController MapSelect() =>
            Object.FindFirstObjectByType<MapSelectController>(FindObjectsInactive.Include);

        private static CharacterSelectController CharacterSelect() =>
            Object.FindFirstObjectByType<CharacterSelectController>(FindObjectsInactive.Include);

        private static InGameFlowController RoomGraph() =>
            Object.FindFirstObjectByType<InGameFlowController>(FindObjectsInactive.Include);

        private static readonly string[] MapPointNames =
            { "MapPoint_1_SmallCastle", "MapPoint_2_CentralCastle", "MapPoint_3_Fortress" };

        private static Button ActiveMapPointButton(MapSelectController mapSelect) =>
            MapPointNames
                .Select(name => mapSelect.transform.Find(name).GetComponent<Button>())
                .First(b => b.interactable);

        private static RunState NewRun()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            return RunStateFactory.Create(map, new RunConfig { startingArmyCount = 1 });
        }

        [UnityTest]
        public IEnumerator Load_NoPendingRun_ShowsOnlyMapSelectPanel()
        {
            yield return LoadScene();

            Assert.IsTrue(MapSelect().gameObject.activeSelf, "새 게임 진입은 맵 선택부터 시작해야 함");
            Assert.IsFalse(CharacterSelect().gameObject.activeSelf);
            Assert.IsFalse(RoomGraph().gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Load_WithPendingRun_ShowsOnlyRoomGraphAndConsumesPendingRun()
        {
            RunSessionContext.SetPendingRun(NewRun());
            // "인게임 → 맵 선택" 전환의 로딩 오버레이가 낮은 확률(기본 15%)로 파편 클릭 대기에 들어가면
            // 이 테스트의 "1프레임 뒤 방 그래프가 곧장 보인다" 가정이 깨진다 — 결정적으로 만든다.
            LoadingOverlayPanel.GetOrCreate().FragmentTriggerChance = 0f;

            yield return LoadScene();

            Assert.IsFalse(MapSelect().gameObject.activeSelf);
            Assert.IsFalse(CharacterSelect().gameObject.activeSelf);
            Assert.IsTrue(RoomGraph().gameObject.activeSelf,
                "이어하기는 맵/캐릭터 선택을 건너뛰고 방 그래프로 곧장 진입해야 함");
            Assert.IsNull(RunSessionContext.PendingRun, "한 번 소비된 PendingRun은 다시 남아있으면 안 됨");
        }

        [UnityTest]
        public IEnumerator MapConfirmed_SwitchesToCharacterSelectPanelOnly()
        {
            yield return LoadScene();

            MapSelectController mapSelect = MapSelect();
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

            CharacterSelectController characterSelect = CharacterSelect();
            characterSelect.transform.Find("ConfirmButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsFalse(MapSelect().gameObject.activeSelf);
            Assert.IsFalse(characterSelect.gameObject.activeSelf);
            Assert.IsTrue(RoomGraph().gameObject.activeSelf,
                "캐릭터 확정 후에는 방 그래프로 전환돼야 함");
        }
    }
}
