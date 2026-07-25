using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 맵 생성 불변식 검증 (상세 기획 §5.3). 무작위 알고리즘이므로 여러 시드에 대해 반복 검증한다.
    /// </summary>
    public class MapGeneratorTests
    {
        private const int SeedSamples = 30;

        private static MapGenerationConfig DefaultConfig() => new MapGenerationConfig();

        private static MapState Generate(int seed, MapGenerationConfig config = null)
            => new MapGenerator(config ?? DefaultConfig(), seed).Generate();

        private static IEnumerable<int> Seeds() => Enumerable.Range(0, SeedSamples);

        // ── 생성자 검증 ──────────────────────────────────────────────

        [Test]
        public void Constructor_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MapGenerator(null, 0));
        }

        [Test]
        public void Constructor_TooFewFloors_ThrowsArgumentException()
        {
            var config = DefaultConfig();
            config.floorCount = 3;
            Assert.Throws<ArgumentException>(() => new MapGenerator(config, 0));
        }

        [Test]
        public void Constructor_StartingNodesBelowTwo_ThrowsArgumentException()
        {
            var config = DefaultConfig();
            config.startingNodeCount = new IntRange(1, 3);
            Assert.Throws<ArgumentException>(() => new MapGenerator(config, 0));
        }

        // ── 기본 구조 ────────────────────────────────────────────────

        [Test]
        public void Generate_DefaultConfig_ReturnsNonEmptyMap()
        {
            MapState map = Generate(seed: 42);

            Assert.IsNotNull(map);
            Assert.IsNotEmpty(map.nodes);
            Assert.AreEqual("Map01", map.configName);
            Assert.AreEqual(42, map.seed);
            Assert.IsEmpty(map.visitedPath, "새로 생성된 맵은 방문 기록이 없어야 한다");
        }

        [Test]
        public void Generate_AllNodesHaveConnections_NoOrphans()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                int topFloor = DefaultConfig().floorCount - 1;

                foreach (MapNode node in map.nodes)
                {
                    Assert.IsFalse(node.HasNoConnections(), $"seed {seed}: 고아 노드 {node.point}");
                    if (node.point.y > 0)
                        Assert.IsNotEmpty(node.incoming, $"seed {seed}: {node.point} 노드에 진입 경로 없음");
                    if (node.point.y < topFloor)
                        Assert.IsNotEmpty(node.outgoing, $"seed {seed}: {node.point} 노드에 진출 경로 없음");
                }
            }
        }

        [Test]
        public void Generate_EveryEdgeGoesExactlyOneFloorUp()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                foreach (MapNode node in map.nodes)
                    foreach (GridPoint dest in node.outgoing)
                        Assert.AreEqual(node.point.y + 1, dest.y,
                            $"seed {seed}: {node.point} → {dest} 연결이 한 층 위가 아님");
            }
        }

        // ── 고정 층 규칙 (§5.3) ─────────────────────────────────────

        [Test]
        public void Generate_FirstFloor_IsAllNormalBattle()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                var firstFloor = map.nodes.Where(n => n.point.y == 0).ToList();

                Assert.GreaterOrEqual(firstFloor.Count, 2, $"seed {seed}: 시작 노드는 최소 2개");
                Assert.IsTrue(firstFloor.All(n => n.roomType == RoomType.NormalBattle),
                    $"seed {seed}: 1층은 전부 일반전투여야 함");
            }
        }

        [Test]
        public void Generate_TopFloor_IsSingleBossNode()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                int topFloor = DefaultConfig().floorCount - 1;
                var topNodes = map.nodes.Where(n => n.point.y == topFloor).ToList();

                Assert.AreEqual(1, topNodes.Count, $"seed {seed}: 최상층 노드는 1개(보스)여야 함");
                Assert.AreEqual(RoomType.Boss, topNodes[0].roomType, $"seed {seed}");
                Assert.AreEqual(1, map.nodes.Count(n => n.roomType == RoomType.Boss),
                    $"seed {seed}: 보스 노드는 맵 전체에 1개여야 함");
            }
        }

        [Test]
        public void Generate_PreBossFloor_IsAllRest()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                int preBossFloor = DefaultConfig().floorCount - 2;
                var preBossNodes = map.nodes.Where(n => n.point.y == preBossFloor).ToList();

                Assert.IsNotEmpty(preBossNodes, $"seed {seed}");
                Assert.IsTrue(preBossNodes.All(n => n.roomType == RoomType.Rest),
                    $"seed {seed}: 보스 직전 층은 전부 증원이어야 함");
            }
        }

        [Test]
        public void Generate_SecondFloor_IsBattleOrEventOnly()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                var secondFloor = map.nodes.Where(n => n.point.y == 1).ToList();

                Assert.IsTrue(secondFloor.All(
                        n => n.roomType == RoomType.NormalBattle || n.roomType == RoomType.Event),
                    $"seed {seed}: 2층은 일반전투 또는 이벤트만 허용");
            }
        }

        // ── 배정 제약 (§5.3) ─────────────────────────────────────────

        [Test]
        public void Generate_NoRestToRestEdge()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                foreach (MapNode node in map.nodes.Where(n => n.roomType == RoomType.Rest))
                    foreach (GridPoint dest in node.outgoing)
                        Assert.AreNotEqual(RoomType.Rest, map.GetNode(dest).roomType,
                            $"seed {seed}: 증원 연속 {node.point} → {dest}");
            }
        }

        [Test]
        public void Generate_TwoWayBranch_DestinationTypesDiffer()
        {
            // 확률 배정 층(2층~증원층 직전)으로 갈라지는 2갈래 분기는 목적지 타입이 서로 달라야 한다.
            // 3갈래는 제약상 중복이 불가피할 수 있어 검증 대상에서 제외.
            int lastProbabilityFloor = DefaultConfig().floorCount - 3;

            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                foreach (MapNode node in map.nodes.Where(n => n.outgoing.Count == 2))
                {
                    if (node.outgoing.Any(d => d.y < 1 || d.y > lastProbabilityFloor))
                        continue;

                    RoomType typeA = map.GetNode(node.outgoing[0]).roomType;
                    RoomType typeB = map.GetNode(node.outgoing[1]).roomType;
                    Assert.AreNotEqual(typeA, typeB,
                        $"seed {seed}: {node.point}의 분기 목적지 타입이 중복됨 ({typeA})");
                }
            }
        }

        [Test]
        public void Generate_NoCrossingConnections()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                foreach (MapNode node in map.nodes)
                {
                    var right = map.GetNode(new GridPoint(node.point.x + 1, node.point.y));
                    if (right == null) continue;

                    bool nodeToTopRight = node.outgoing.Contains(new GridPoint(node.point.x + 1, node.point.y + 1));
                    bool rightToTop = right.outgoing.Contains(new GridPoint(node.point.x, node.point.y + 1));
                    Assert.IsFalse(nodeToTopRight && rightToTop,
                        $"seed {seed}: {node.point} 부근에서 경로 교차 발생");
                }
            }
        }

        // ── 도달성 ──────────────────────────────────────────────────

        [Test]
        public void Generate_BossIsReachableFromEveryStartingNode()
        {
            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed);
                MapNode boss = map.GetBossNode();
                Assert.IsNotNull(boss, $"seed {seed}");

                foreach (MapNode start in map.nodes.Where(n => n.point.y == 0))
                {
                    var visited = new HashSet<GridPoint>();
                    var queue = new Queue<GridPoint>();
                    queue.Enqueue(start.point);

                    while (queue.Count > 0)
                    {
                        GridPoint current = queue.Dequeue();
                        if (!visited.Add(current)) continue;
                        foreach (GridPoint next in map.GetNode(current).outgoing)
                            queue.Enqueue(next);
                    }

                    Assert.IsTrue(visited.Contains(boss.point),
                        $"seed {seed}: 시작 노드 {start.point}에서 보스 도달 불가");
                }
            }
        }

        // ── 결정성 / config 반영 ─────────────────────────────────────

        [Test]
        public void Generate_SameSeed_ProducesIdenticalMap()
        {
            string first = Generate(seed: 7).ToJson();
            string second = Generate(seed: 7).ToJson();
            Assert.AreEqual(first, second, "같은 시드는 같은 맵을 만들어야 한다 (이어하기 전제)");
        }

        [Test]
        public void Generate_DifferentSeeds_ProduceDifferentMaps()
        {
            var jsons = Enumerable.Range(0, 5).Select(s => Generate(s).ToJson()).Distinct().ToList();
            Assert.Greater(jsons.Count, 1, "서로 다른 시드가 전부 같은 맵을 만들면 안 된다");
        }

        [Test]
        public void Generate_CustomFloorCountAndWidth_AreRespected()
        {
            var config = DefaultConfig();
            config.floorCount = 6;
            config.gridWidth = 4;

            foreach (int seed in Seeds())
            {
                MapState map = Generate(seed, config);

                Assert.AreEqual(config.floorCount - 1, map.nodes.Max(n => n.point.y),
                    $"seed {seed}: 최상층 인덱스가 floorCount와 불일치");
                Assert.IsTrue(map.nodes.All(n => n.point.x < config.gridWidth),
                    $"seed {seed}: gridWidth 초과 노드 존재");

                MapNode boss = map.GetBossNode();
                Assert.IsNotNull(boss, $"seed {seed}");
                Assert.AreEqual(config.floorCount - 1, boss.point.y, $"seed {seed}");
            }
        }

        [Test]
        public void Generate_NodeIds_AreUniqueAndMatchPoints()
        {
            MapState map = Generate(seed: 3);

            Assert.AreEqual(map.nodes.Count, map.nodes.Select(n => n.id).Distinct().Count(),
                "노드 id는 유일해야 한다 (BattleSetupData.roomId로 사용)");
            Assert.IsTrue(map.nodes.All(n => n.id == $"room_{n.point.x}_{n.point.y}"));
        }
    }
}
