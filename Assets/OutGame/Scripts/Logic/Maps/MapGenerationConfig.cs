using System;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// 맵 생성 밸런스 값 — 코드 수정 없이 조정 가능해야 함 (상세 기획 §4-1 확정 요구).
    /// M2에서 ScriptableObject(MapConfig 에셋)가 이 POCO를 채워 넘긴다.
    /// </summary>
    [Serializable]
    public class MapGenerationConfig
    {
        public string mapName = "Map01";

        public int floorCount = 10;                                // 층 수 (최소 4)
        public int gridWidth = 5;                                  // 층당 최대 노드 수
        public int pathCount = 5;                                  // 상향 경로 수
        public IntRange startingNodeCount = new IntRange(2, 3);    // 1층 시작 노드 수 (최소 2)
        public IntRange preBossNodeCount = new IntRange(2, 3);     // 보스 직전 층 노드 수

        // 방 타입 확률 — 고정층 제외 나머지 층 (상세 기획 §5.3 초안: 55/30/15)
        public float battleWeight = 55f;
        public float eventWeight = 30f;
        public float restWeight = 15f;

        // 뷰 배치 참고용 레이아웃 값
        public float layerDistance = 2f;            // 층 간 세로 거리
        public float nodesApartDistance = 1f;       // 같은 층 노드 가로 간격
        public float positionRandomization = 0.3f;  // 0 = 정렬, 1 = 최대 흐트러짐

        public void Validate()
        {
            if (floorCount < 4)
                throw new ArgumentException($"floorCount는 4 이상이어야 합니다 (1층 전투 + 중간 + 휴식층 + 보스층). 현재: {floorCount}");
            if (gridWidth < 2)
                throw new ArgumentException($"gridWidth는 2 이상이어야 합니다. 현재: {gridWidth}");
            if (pathCount < 1)
                throw new ArgumentException($"pathCount는 1 이상이어야 합니다. 현재: {pathCount}");
            if (startingNodeCount.min < 2 || startingNodeCount.min > startingNodeCount.max || startingNodeCount.max > gridWidth)
                throw new ArgumentException($"startingNodeCount는 2 ≤ min ≤ max ≤ gridWidth 여야 합니다. 현재: [{startingNodeCount.min},{startingNodeCount.max}]");
            if (preBossNodeCount.min < 1 || preBossNodeCount.min > preBossNodeCount.max || preBossNodeCount.max > gridWidth)
                throw new ArgumentException($"preBossNodeCount는 1 ≤ min ≤ max ≤ gridWidth 여야 합니다. 현재: [{preBossNodeCount.min},{preBossNodeCount.max}]");
            if (battleWeight <= 0f || eventWeight < 0f || restWeight < 0f)
                throw new ArgumentException($"확률 가중치는 battle > 0, event/rest ≥ 0 이어야 합니다. 현재: {battleWeight}/{eventWeight}/{restWeight}");
        }
    }
}
