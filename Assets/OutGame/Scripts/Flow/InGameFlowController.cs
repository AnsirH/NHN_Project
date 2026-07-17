using System;
using OutGame.Logic.Maps;
using OutGame.UI;
using UnityEngine;

namespace OutGame.Flow
{
    /// <summary>
    /// M2 방 진행 루프: 맵 생성 → 방 그래프 표시 → 노드 선택 → 방문 확정 → 더미 방 → 복귀.
    /// M3부터 전투 방 분기(배치 UI), M5부터 RunState 저장이 이 흐름에 붙는다.
    /// </summary>
    public class InGameFlowController : MonoBehaviour
    {
        [SerializeField] private RoomMapPanel mapPanel;
        [SerializeField] private DummyRoomPanel roomPanel;
        [SerializeField] private RoomTypeVisualSet visuals;

        [Header("개발용 맵 생성 설정")]
        [SerializeField] private int seed = 42;
        [SerializeField] private bool randomizeSeed;

        private MapState map;

        private void Start()
        {
            if (visuals == null)
                visuals = Resources.Load<RoomTypeVisualSet>("OutGame/RoomTypeVisuals"); // 배선 누락 대비 폴백

            if (mapPanel == null || roomPanel == null || visuals == null)
                throw new InvalidOperationException(
                    "InGameFlowController의 mapPanel/roomPanel/visuals가 배선되지 않았습니다 — 씬 구성(SceneSetupM2) 확인");

            int usedSeed = randomizeSeed ? Environment.TickCount : seed;
            map = new MapGenerator(new MapGenerationConfig(), usedSeed).Generate();

            mapPanel.RoomSelected += OnRoomSelected;
            roomPanel.Completed += OnRoomCompleted;

            roomPanel.Hide();
            mapPanel.Open(map);
        }

        private void OnDestroy()
        {
            if (mapPanel != null) mapPanel.RoomSelected -= OnRoomSelected;
            if (roomPanel != null) roomPanel.Completed -= OnRoomCompleted;
        }

        private void OnRoomSelected(MapNode node)
        {
            MapProgress.Visit(map, node.point);
            mapPanel.Refresh();

            if (MapProgress.HasVisitedBoss(map))
                roomPanel.ShowRunClear();
            else
                roomPanel.Show(node, visuals);
        }

        private void OnRoomCompleted()
        {
            mapPanel.Refresh();
        }
    }
}
