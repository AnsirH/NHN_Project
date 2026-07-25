using System;
using System.Collections.Generic;
using System.Linq;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// 방 타입 배정 (상세 기획 §5.3).
    /// 고정층: 1층=일반전투, 최상층=보스, 직전 층=휴식.
    /// 나머지 층: 확률표(전투/이벤트/휴식) + 제약(휴식 연속 금지, 2층은 전투/이벤트,
    /// 한 노드에서 갈라지는 분기의 목적지 타입 중복 금지 — 만족 불가능할 때만 완화).
    /// </summary>
    public static class RoomTypeAssigner
    {
        public static void Assign(List<MapNode> nodes, MapGenerationConfig config, Random rng)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int topFloor = config.floorCount - 1;
            Dictionary<GridPoint, MapNode> byPoint = nodes.ToDictionary(n => n.point);
            var assigned = new HashSet<GridPoint>();

            // 아래층부터 배정 — 선행 노드(휴식 연속)와 먼저 배정된 형제(분기 중복)를 참조할 수 있다
            foreach (MapNode node in nodes.OrderBy(n => n.point.y).ThenBy(n => n.point.x))
            {
                node.roomType = PickType(node, config, byPoint, assigned, rng, topFloor);
                assigned.Add(node.point);
            }
        }

        private static RoomType PickType(
            MapNode node,
            MapGenerationConfig config,
            Dictionary<GridPoint, MapNode> byPoint,
            HashSet<GridPoint> assigned,
            Random rng,
            int topFloor)
        {
            int y = node.point.y;
            if (y == 0) return RoomType.NormalBattle;
            if (y == topFloor) return RoomType.Boss;
            if (y == topFloor - 1) return RoomType.Rest;

            List<RoomType> allowed = BuildAllowedTypes(node, config, byPoint, assigned, y, topFloor);
            return WeightedPick(allowed, config, rng);
        }

        private static List<RoomType> BuildAllowedTypes(
            MapNode node,
            MapGenerationConfig config,
            Dictionary<GridPoint, MapNode> byPoint,
            HashSet<GridPoint> assigned,
            int y,
            int topFloor)
        {
            var forbidden = new HashSet<RoomType>();

            // 가중치 0 = config로 비활성화된 타입 (battle은 Validate가 양수를 보장)
            if (config.eventWeight <= 0f) forbidden.Add(RoomType.Event);
            if (config.restWeight <= 0f) forbidden.Add(RoomType.Rest);
            if (config.augmentWeight <= 0f) forbidden.Add(RoomType.Augment);

            // 2층은 전투/이벤트만 허용 (§5.3) — 휴식뿐 아니라 증강도 제외
            if (y == 1)
            {
                forbidden.Add(RoomType.Rest);
                forbidden.Add(RoomType.Augment);
            }
            // 고정 휴식층 직전 층은 휴식 금지 (연속 방지)
            if (y == topFloor - 2)
                forbidden.Add(RoomType.Rest);

            // 선행 노드가 휴식이면 휴식 금지 (휴식 연속 금지)
            foreach (GridPoint predPoint in node.incoming)
            {
                if (byPoint.TryGetValue(predPoint, out MapNode pred) && pred.roomType == RoomType.Rest)
                    forbidden.Add(RoomType.Rest);
            }

            List<RoomType> baseAllowed = CandidatesExcept(forbidden);

            // 분기 목적지 중복 금지: 같은 선행 노드에서 이미 배정된 형제의 타입을 제외
            var withSiblings = new HashSet<RoomType>(forbidden);
            foreach (GridPoint predPoint in node.incoming)
            {
                if (!byPoint.TryGetValue(predPoint, out MapNode pred)) continue;
                foreach (GridPoint siblingPoint in pred.outgoing)
                {
                    if (siblingPoint.Equals(node.point) || !assigned.Contains(siblingPoint)) continue;
                    withSiblings.Add(byPoint[siblingPoint].roomType);
                }
            }

            List<RoomType> strict = CandidatesExcept(withSiblings);
            return strict.Count > 0 ? strict : baseAllowed; // 만족 불가능하면 분기 제약만 완화
        }

        private static List<RoomType> CandidatesExcept(HashSet<RoomType> forbidden)
        {
            var candidates = new List<RoomType>(4);
            if (!forbidden.Contains(RoomType.NormalBattle)) candidates.Add(RoomType.NormalBattle);
            if (!forbidden.Contains(RoomType.Event)) candidates.Add(RoomType.Event);
            if (!forbidden.Contains(RoomType.Rest)) candidates.Add(RoomType.Rest);
            if (!forbidden.Contains(RoomType.Augment)) candidates.Add(RoomType.Augment);
            return candidates;
        }

        private static RoomType WeightedPick(List<RoomType> allowed, MapGenerationConfig config, Random rng)
        {
            if (allowed.Count == 0)
                return RoomType.NormalBattle; // BuildAllowedTypes가 비지 않음을 보장 — 방어용

            float total = allowed.Sum(t => WeightOf(t, config));
            if (total <= 0f)
                return allowed[0]; // 허용 타입 전부 가중치 0 — 허용 목록 밖 타입을 반환하면 안 됨

            double roll = rng.NextDouble() * total;
            foreach (RoomType type in allowed)
            {
                roll -= WeightOf(type, config);
                if (roll < 0)
                    return type;
            }

            return allowed[allowed.Count - 1];
        }

        private static float WeightOf(RoomType type, MapGenerationConfig config)
        {
            switch (type)
            {
                case RoomType.NormalBattle: return config.battleWeight;
                case RoomType.Event: return config.eventWeight;
                case RoomType.Rest: return config.restWeight;
                case RoomType.Augment: return config.augmentWeight;
                default: return 0f;
            }
        }
    }
}
