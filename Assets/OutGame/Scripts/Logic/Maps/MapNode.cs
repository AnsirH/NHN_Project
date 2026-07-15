using System;
using System.Collections.Generic;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// 방 그래프의 노드 하나. id는 인게임 인터페이스(BattleSetupData.roomId)로 전달된다.
    /// </summary>
    [Serializable]
    public class MapNode
    {
        public string id;
        public RoomType roomType;
        public GridPoint point;

        // 뷰 배치 참고용 좌표 (생성 시 확정 — 이어하기 시 동일 레이아웃 보장)
        public float posX;
        public float posY;

        public List<GridPoint> incoming = new List<GridPoint>();
        public List<GridPoint> outgoing = new List<GridPoint>();

        public MapNode()
        {
        }

        public MapNode(RoomType roomType, GridPoint point)
        {
            this.roomType = roomType;
            this.point = point;
            id = $"room_{point.x}_{point.y}";
        }

        public void AddIncoming(GridPoint p)
        {
            if (!incoming.Contains(p))
                incoming.Add(p);
        }

        public void AddOutgoing(GridPoint p)
        {
            if (!outgoing.Contains(p))
                outgoing.Add(p);
        }

        public void RemoveIncoming(GridPoint p) => incoming.RemoveAll(e => e.Equals(p));

        public void RemoveOutgoing(GridPoint p) => outgoing.RemoveAll(e => e.Equals(p));

        public bool HasNoConnections() => incoming.Count == 0 && outgoing.Count == 0;
    }
}
