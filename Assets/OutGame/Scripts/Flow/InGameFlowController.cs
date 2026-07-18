using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OutGame.Logic.Events;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutGame.Flow
{
    /// <summary>
    /// 인게임 방 진행 루프: 맵 생성 → 방 그래프 표시 → 노드 선택 → 방문 확정 → 방 타입별 패널 → 복귀.
    /// 전투/보스 방은 아직 더미 패널(M6에서 배치 UI+더미 전투 브릿지로 대체 예정, §8).
    /// </summary>
    public class InGameFlowController : MonoBehaviour
    {
        [SerializeField] private RoomMapPanel mapPanel;
        [SerializeField] private DummyRoomPanel roomPanel; // 보스 클리어 표시 + 전투 방 임시 자리 (M6까지)
        [SerializeField] private EventPanel eventPanel;
        [SerializeField] private RestPanel restPanel;
        [SerializeField] private RoomTypeVisualSet visuals;
        [SerializeField] private RunConfigAsset runConfig;

        [Header("개발용 맵 생성 설정 — MapSelect 없이 씬을 단독 실행할 때만 사용 (§5.2)")]
        [SerializeField] private int seed = 42;
        [SerializeField] private bool randomizeSeed;

        /// <summary>테스트/툴링에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅.</summary>
        public Action<string> LoadSceneAction = SceneManager.LoadScene;

        private RunState run;
        private System.Random rng;
        private List<EventData> eventDataPool;
        private Dictionary<string, EventDefinition> eventDefsById;
        private Dictionary<string, ArmyDefinition> armyDefsById;
        private string savePath;
        private bool runEnded;

        private void Start()
        {
            // 이후 검증에서 예외가 나더라도 PendingRun이 stale 상태로 남지 않도록 가장 먼저 소비한다.
            RunState pendingRun = RunSessionContext.ConsumePendingRun();

            if (visuals == null)
                visuals = Resources.Load<RoomTypeVisualSet>("OutGame/RoomTypeVisuals"); // 배선 누락 대비 폴백
            if (runConfig == null)
                runConfig = Resources.Load<RunConfigAsset>("OutGame/Data/RunConfig_Default");

            if (mapPanel == null || roomPanel == null || eventPanel == null || restPanel == null
                || visuals == null || runConfig == null)
                throw new InvalidOperationException(
                    "InGameFlowController의 필수 참조가 배선되지 않았습니다 — 씬 구성(SceneSetupM2/M4) 확인");

            List<EventDefinition> eventPool = Resources.LoadAll<EventDefinition>("OutGame/Data/Events").ToList();
            if (eventPool.Count == 0)
                throw new InvalidOperationException("이벤트 정의를 찾을 수 없습니다 — SceneSetupM4Data.Run() 실행 필요");
            eventDataPool = eventPool.Select(e => e.ToData()).ToList(); // 한 번만 변환해 캐시 (매 방문마다 재파싱 방지)
            eventDefsById = eventPool.ToDictionary(e => e.ToData().id);

            armyDefsById = Resources.LoadAll<ArmyDefinition>("OutGame/Data")
                .ToDictionary(a => a.ToData().id);

            savePath = RunSaveService.DefaultPath;
            run = pendingRun;
            if (run == null)
            {
                // MainMenu/MapSelect를 거치지 않고 이 씬을 단독 실행했을 때의 개발용 폴백
                int usedSeed = randomizeSeed ? Environment.TickCount : seed;
                MapState map = new MapGenerator(new MapGenerationConfig(), usedSeed).Generate();
                run = RunStateFactory.Create(map, runConfig.ToData());
            }
            rng = new System.Random(Environment.TickCount);

            mapPanel.RoomSelected += OnRoomSelected;
            roomPanel.Completed += OnRoomCompleted;
            eventPanel.Completed += OnRoomCompleted;
            restPanel.Completed += OnRoomCompleted;

            roomPanel.Hide();
            mapPanel.Open(run.mapState);
        }

        private void OnDestroy()
        {
            if (mapPanel != null) mapPanel.RoomSelected -= OnRoomSelected;
            if (roomPanel != null) roomPanel.Completed -= OnRoomCompleted;
            if (eventPanel != null) eventPanel.Completed -= OnRoomCompleted;
            if (restPanel != null) restPanel.Completed -= OnRoomCompleted;
        }

        private void OnRoomSelected(MapNode node)
        {
            MapProgress.Visit(run.mapState, node.point);
            mapPanel.Refresh();

            if (MapProgress.HasVisitedBoss(run.mapState))
            {
                runEnded = true;
                RunSaveService.DeleteSave(savePath); // 런 종료 — 이어하기 대상에서 제외 (§5.1)
                roomPanel.ShowRunClear();
                return;
            }

            // 저장은 방 결과(보상 적용 등)까지 반영된 뒤 OnRoomCompleted에서 한 번만 수행한다.
            // 여기서 먼저 저장하면 "방문함"만 기록되고 보상은 누락된 상태로 저장될 위험이 있다.
            switch (node.roomType)
            {
                case RoomType.Event:
                    OpenEventRoom();
                    break;
                case RoomType.Rest:
                    restPanel.Open(run, armyDefsById);
                    break;
                default:
                    roomPanel.Show(node, visuals); // 전투 방 — 배치 UI 연결은 M6 (§8)
                    break;
            }
        }

        private void OpenEventRoom()
        {
            EventData selected = EventSelector.SelectRandom(eventDataPool, run.visitedEventIds, rng);
            if (!run.visitedEventIds.Contains(selected.id))
                run.visitedEventIds.Add(selected.id); // 소진 후 재추첨된 반복 항목은 중복 기록하지 않음

            eventPanel.Open(eventDefsById[selected.id], run);
        }

        private void OnRoomCompleted()
        {
            if (runEnded)
            {
                LoadSceneAction(SceneNames.MainMenu); // 런 클리어 화면의 [완료] → 메인 메뉴 복귀
                return;
            }

            mapPanel.Refresh();
            SaveProgress();
        }

        /// <summary>
        /// 방 진행 상황을 즉시 저장한다. 클라우드 동기화 잠금 등으로 인한 일시적 저장 실패가
        /// 게임 진행 자체를 막지 않도록 예외를 흡수하고 경고만 남긴다.
        /// </summary>
        private void SaveProgress()
        {
            try
            {
                RunSaveService.Save(run, savePath);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[InGameFlowController] 진행 저장 실패 — {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning($"[InGameFlowController] 진행 저장 실패 — {e.Message}");
            }
        }
    }
}
