using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// 생성된 방 그래프 + 방문 기록 (상세 기획 §6). RunState 저장 데이터에 포함된다.
    /// </summary>
    [Serializable]
    public class MapState
    {
        public string configName;
        public int seed;
        public List<MapNode> nodes = new List<MapNode>();
        public List<GridPoint> visitedPath = new List<GridPoint>();

        public MapNode GetNode(GridPoint point) => nodes.FirstOrDefault(n => n.point.Equals(point));

        public MapNode GetBossNode() => nodes.FirstOrDefault(n => n.roomType == RoomType.Boss);

        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true);

        public static MapState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("MapState JSON이 비어 있습니다.", nameof(json));

            MapState state;
            try
            {
                state = JsonUtility.FromJson<MapState>(json);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"MapState JSON 파싱 실패: {e.Message}", nameof(json), e);
            }

            if (state == null || state.nodes == null || state.nodes.Count == 0)
                throw new ArgumentException("MapState JSON에 노드 데이터가 없습니다.", nameof(json));

            return state;
        }
    }
}
