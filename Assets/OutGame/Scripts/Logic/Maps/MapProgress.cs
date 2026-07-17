using System;
using System.Collections.Generic;
using System.Linq;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// 방 진행 규칙 (상세 기획 §5.3 진행 규칙).
    /// 현재 위치에서 선으로 연결된 다음 층 노드만 선택 가능. 방문 기록은 MapState.visitedPath에 쌓인다.
    /// </summary>
    public static class MapProgress
    {
        /// <summary>현재 위치 노드. 방문 기록이 없으면 null (런 시작 직후 — 1층 선택 대기).</summary>
        public static MapNode GetCurrentNode(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (map.visitedPath.Count == 0) return null;

            return map.GetNode(map.visitedPath[map.visitedPath.Count - 1]);
        }

        /// <summary>지금 선택 가능한 노드 목록. 시작 전이면 1층 노드 전체, 이후엔 현재 노드의 outgoing.</summary>
        public static List<MapNode> GetSelectableNodes(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            MapNode current = GetCurrentNode(map);
            if (current == null)
                return map.nodes.Where(n => n.point.y == 0).ToList();

            return current.outgoing
                .Select(map.GetNode)
                .Where(n => n != null)
                .ToList();
        }

        public static bool CanVisit(MapState map, GridPoint point)
        {
            return GetSelectableNodes(map).Any(n => n.point.Equals(point));
        }

        /// <summary>노드 방문 처리. 선택 불가능한 노드면 InvalidOperationException.</summary>
        public static void Visit(MapState map, GridPoint point)
        {
            if (!CanVisit(map, point))
                throw new InvalidOperationException(
                    $"선택 불가능한 노드입니다: {point} (현재 위치: {GetCurrentNode(map)?.point.ToString() ?? "시작 전"})");

            map.visitedPath.Add(point);
        }

        /// <summary>보스 방을 방문했는가 (런 클리어 판정의 전제).</summary>
        public static bool HasVisitedBoss(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            MapNode boss = map.GetBossNode();
            return boss != null && map.visitedPath.Contains(boss.point);
        }
    }
}
