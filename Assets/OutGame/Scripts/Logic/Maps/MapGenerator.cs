using System;
using System.Collections.Generic;
using System.Linq;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// STS 맵 생성 알고리즘 (silverua/slay-the-spire-map-in-unity, MIT 이식).
    /// 격자 생성 → 상향 경로 → 교차 제거 → 고아 노드 제거 → 방 타입 배정(상세 기획 §5.3).
    /// 시드 기반 결정적 생성 — 같은 (config, seed)는 항상 같은 맵을 만든다.
    /// 주의: System.Random의 시드 수열은 런타임(Mono/IL2CPP/.NET 버전) 간 동일함이 보장되지 않으므로,
    /// 이어하기는 시드 재생성이 아니라 MapState 전체 직렬화(§6)로 복원한다.
    /// </summary>
    public class MapGenerator
    {
        private readonly MapGenerationConfig config;
        private readonly int seed;

        public MapGenerator(MapGenerationConfig config, int seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();

            this.config = config;
            this.seed = seed;
        }

        public MapState Generate()
        {
            var rng = new Random(seed);

            List<List<MapNode>> grid = PlaceGridNodes();
            List<List<GridPoint>> paths = GeneratePaths(rng);
            SetUpConnections(grid, paths);
            RemoveCrossConnections(grid, rng);

            List<MapNode> connected = grid.SelectMany(layer => layer)
                .Where(n => !n.HasNoConnections())
                .ToList();

            RandomizePositions(connected, rng);
            RoomTypeAssigner.Assign(connected, config, rng);

            return new MapState
            {
                configName = config.mapName,
                seed = seed,
                nodes = connected,
            };
        }

        // ── 1. 격자 배치 ─────────────────────────────────────────────

        private List<List<MapNode>> PlaceGridNodes()
        {
            var grid = new List<List<MapNode>>();
            float offset = config.nodesApartDistance * config.gridWidth / 2f;

            for (int y = 0; y < config.floorCount; y++)
            {
                var layer = new List<MapNode>();
                for (int x = 0; x < config.gridWidth; x++)
                {
                    layer.Add(new MapNode(RoomType.NormalBattle, new GridPoint(x, y))
                    {
                        posX = -offset + x * config.nodesApartDistance,
                        posY = y * config.layerDistance,
                    });
                }
                grid.Add(layer);
            }

            return grid;
        }

        private void RandomizePositions(List<MapNode> nodes, Random rng)
        {
            foreach (MapNode node in nodes)
            {
                float xJitter = ((float)rng.NextDouble() - 0.5f) * config.nodesApartDistance;
                float yJitter = ((float)rng.NextDouble() - 0.5f) * config.layerDistance;
                node.posX += xJitter * config.positionRandomization;
                node.posY += yJitter * config.positionRandomization;
            }
        }

        // ── 2. 상향 경로 생성 ────────────────────────────────────────

        private GridPoint GetFinalPoint(Random rng)
        {
            int y = config.floorCount - 1;
            if (config.gridWidth % 2 == 1)
                return new GridPoint(config.gridWidth / 2, y);

            return rng.Next(2) == 0
                ? new GridPoint(config.gridWidth / 2, y)
                : new GridPoint(config.gridWidth / 2 - 1, y);
        }

        private List<List<GridPoint>> GeneratePaths(Random rng)
        {
            GridPoint finalPoint = GetFinalPoint(rng);
            int startingCount = config.startingNodeCount.GetValue(rng);
            int preBossCount = config.preBossNodeCount.GetValue(rng);

            List<int> candidateXs = Enumerable.Range(0, config.gridWidth).ToList();

            candidateXs.Shuffle(rng);
            List<GridPoint> startingPoints = candidateXs.Take(startingCount)
                .Select(x => new GridPoint(x, 0)).ToList();

            candidateXs.Shuffle(rng);
            List<GridPoint> preBossPoints = candidateXs.Take(preBossCount)
                .Select(x => new GridPoint(x, finalPoint.y - 1)).ToList();

            // 모든 시작/직전 노드가 최소 1회 사용되도록 경로 수를 보정
            int numOfPaths = Math.Max(Math.Max(startingCount, preBossCount), config.pathCount);

            var paths = new List<List<GridPoint>>();
            for (int i = 0; i < numOfPaths; i++)
            {
                List<GridPoint> path = BuildPath(startingPoints[i % startingCount], preBossPoints[i % preBossCount], rng);
                path.Add(finalPoint);
                paths.Add(path);
            }

            return paths;
        }

        /// <summary>아래에서 위로 한 층씩, 좌우 ±1 칸 내에서 무작위 이동하며 경로를 만든다.</summary>
        private List<GridPoint> BuildPath(GridPoint from, GridPoint to, Random rng)
        {
            int lastCol = from.x;
            var path = new List<GridPoint> { from };
            var candidates = new List<int>();

            for (int row = 1; row < to.y; row++)
            {
                candidates.Clear();
                int verticalDistance = to.y - row;

                for (int col = lastCol - 1; col <= lastCol + 1; col++)
                {
                    // 남은 층 수 안에 목적지 열까지 도달 가능한 열만 후보로 남긴다
                    if (col >= 0 && col < config.gridWidth && Math.Abs(to.x - col) <= verticalDistance)
                        candidates.Add(col);
                }

                lastCol = candidates.Count > 0
                    ? candidates[rng.Next(candidates.Count)]
                    : lastCol + Math.Sign(to.x - lastCol); // 후보 소진 시 목적지 방향으로 강제 이동

                path.Add(new GridPoint(lastCol, row));
            }

            path.Add(to);
            return path;
        }

        // ── 3. 연결 구성 + 교차 제거 ─────────────────────────────────

        private static void SetUpConnections(List<List<MapNode>> grid, List<List<GridPoint>> paths)
        {
            foreach (List<GridPoint> path in paths)
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    MapNode node = grid[path[i].y][path[i].x];
                    MapNode next = grid[path[i + 1].y][path[i + 1].x];
                    node.AddOutgoing(next.point);
                    next.AddIncoming(node.point);
                }
            }
        }

        /// <summary>
        /// X자로 교차하는 연결 쌍을 찾아 세로 연결로 대체한 뒤, 교차 연결을 확률적으로 제거한다.
        /// </summary>
        private void RemoveCrossConnections(List<List<MapNode>> grid, Random rng)
        {
            for (int x = 0; x < config.gridWidth - 1; x++)
            {
                for (int y = 0; y < config.floorCount - 1; y++)
                {
                    MapNode node = grid[y][x];
                    if (node.HasNoConnections()) continue;
                    MapNode right = grid[y][x + 1];
                    if (right.HasNoConnections()) continue;
                    MapNode top = grid[y + 1][x];
                    if (top.HasNoConnections()) continue;
                    MapNode topRight = grid[y + 1][x + 1];
                    if (topRight.HasNoConnections()) continue;

                    if (!node.outgoing.Contains(topRight.point)) continue;
                    if (!right.outgoing.Contains(top.point)) continue;

                    // 교차 발견 — 세로 직행 연결을 추가해 도달성을 유지
                    node.AddOutgoing(top.point);
                    top.AddIncoming(node.point);
                    right.AddOutgoing(topRight.point);
                    topRight.AddIncoming(right.point);

                    double roll = rng.NextDouble();
                    if (roll < 0.2)
                    {
                        RemoveEdge(node, topRight);
                        RemoveEdge(right, top);
                    }
                    else if (roll < 0.6)
                    {
                        RemoveEdge(node, topRight);
                    }
                    else
                    {
                        RemoveEdge(right, top);
                    }
                }
            }
        }

        private static void RemoveEdge(MapNode fromNode, MapNode toNode)
        {
            fromNode.RemoveOutgoing(toNode.point);
            toNode.RemoveIncoming(fromNode.point);
        }
    }
}
