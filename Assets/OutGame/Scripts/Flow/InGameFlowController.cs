using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Events;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using UnityEngine;

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

        [Header("개발용 맵 생성 설정")]
        [SerializeField] private int seed = 42;
        [SerializeField] private bool randomizeSeed;

        private RunState run;
        private System.Random rng;
        private List<EventData> eventDataPool;
        private Dictionary<string, EventDefinition> eventDefsById;
        private Dictionary<string, ArmyDefinition> armyDefsById;

        private void Start()
        {
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

            int usedSeed = randomizeSeed ? Environment.TickCount : seed;
            rng = new System.Random(usedSeed);

            MapState map = new MapGenerator(new MapGenerationConfig(), usedSeed).Generate();
            run = RunStateFactory.Create(map, runConfig.ToData());

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
                roomPanel.ShowRunClear();
                return;
            }

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
            mapPanel.Refresh();
        }
    }
}
