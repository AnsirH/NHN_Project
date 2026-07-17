using System;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 방 진행 규칙 검증 (상세 기획 §5.3): 연결된 다음 층 노드만 선택 가능, 방문 기록 누적.
    /// </summary>
    public class MapProgressTests
    {
        private static MapState NewMap(int seed = 42)
            => new MapGenerator(new MapGenerationConfig(), seed).Generate();

        [Test]
        public void GetCurrentNode_BeforeFirstVisit_ReturnsNull()
        {
            Assert.IsNull(MapProgress.GetCurrentNode(NewMap()));
        }

        [Test]
        public void GetSelectableNodes_BeforeFirstVisit_ReturnsAllFirstFloorNodes()
        {
            MapState map = NewMap();
            var selectable = MapProgress.GetSelectableNodes(map);
            var expected = map.nodes.Where(n => n.point.y == 0).Select(n => n.point).ToList();

            Assert.That(selectable.Select(n => n.point), Is.EquivalentTo(expected));
        }

        [Test]
        public void Visit_FirstFloorNode_BecomesCurrentNode()
        {
            MapState map = NewMap();
            GridPoint start = map.nodes.First(n => n.point.y == 0).point;

            MapProgress.Visit(map, start);

            Assert.AreEqual(start, MapProgress.GetCurrentNode(map).point);
            Assert.AreEqual(1, map.visitedPath.Count);
        }

        [Test]
        public void GetSelectableNodes_AfterVisit_ReturnsOutgoingOfCurrent()
        {
            MapState map = NewMap();
            MapNode start = map.nodes.First(n => n.point.y == 0);
            MapProgress.Visit(map, start.point);

            var selectable = MapProgress.GetSelectableNodes(map);

            Assert.That(selectable.Select(n => n.point), Is.EquivalentTo(start.outgoing));
        }

        [Test]
        public void Visit_UnconnectedNode_Throws()
        {
            MapState map = NewMap();
            MapNode start = map.nodes.First(n => n.point.y == 0);
            MapProgress.Visit(map, start.point);

            // 다음 층이지만 현재 노드와 연결되지 않은 노드
            MapNode notConnected = map.nodes.FirstOrDefault(
                n => n.point.y == 1 && !start.outgoing.Contains(n.point));
            if (notConnected == null)
                Assert.Ignore("이 시드에서는 2층 노드가 전부 연결됨 — 검증 불가");

            Assert.Throws<InvalidOperationException>(() => MapProgress.Visit(map, notConnected.point));
            Assert.AreEqual(1, map.visitedPath.Count, "실패한 방문은 기록에 남으면 안 된다");
        }

        [Test]
        public void Visit_SameFloorNodeAfterStart_Throws()
        {
            MapState map = NewMap();
            var firstFloor = map.nodes.Where(n => n.point.y == 0).ToList();
            Assume.That(firstFloor.Count, Is.GreaterThanOrEqualTo(2));

            MapProgress.Visit(map, firstFloor[0].point);

            Assert.Throws<InvalidOperationException>(
                () => MapProgress.Visit(map, firstFloor[1].point));
        }

        [Test]
        public void Visit_NonExistentNode_Throws()
        {
            MapState map = NewMap();
            Assert.Throws<InvalidOperationException>(
                () => MapProgress.Visit(map, new GridPoint(99, 99)));
        }

        [Test]
        public void CanVisit_MatchesSelectableNodes()
        {
            MapState map = NewMap();
            MapNode start = map.nodes.First(n => n.point.y == 0);
            MapProgress.Visit(map, start.point);

            foreach (MapNode node in map.nodes)
            {
                bool expected = start.outgoing.Contains(node.point);
                Assert.AreEqual(expected, MapProgress.CanVisit(map, node.point),
                    $"{node.point} CanVisit 불일치");
            }
        }

        [Test]
        public void Visit_WalkToBoss_HasVisitedBossBecomesTrue()
        {
            MapState map = NewMap();
            Assert.IsFalse(MapProgress.HasVisitedBoss(map));

            // 선택 가능 노드 중 첫 번째를 계속 따라가면 반드시 보스에 도달한다 (M1 도달성 보장)
            int guard = 0;
            while (!MapProgress.HasVisitedBoss(map) && guard++ < 100)
            {
                var selectable = MapProgress.GetSelectableNodes(map);
                Assert.IsNotEmpty(selectable, "보스 도달 전에 선택 가능 노드가 없어지면 안 된다");
                MapProgress.Visit(map, selectable[0].point);
            }

            Assert.IsTrue(MapProgress.HasVisitedBoss(map));
            Assert.AreEqual(new MapGenerationConfig().floorCount, map.visitedPath.Count,
                "1층부터 보스층까지 층마다 정확히 1회 방문");
        }

        [Test]
        public void GetSelectableNodes_AfterBoss_ReturnsEmpty()
        {
            MapState map = NewMap();
            int guard = 0;
            while (!MapProgress.HasVisitedBoss(map) && guard++ < 100)
                MapProgress.Visit(map, MapProgress.GetSelectableNodes(map)[0].point);

            Assert.IsEmpty(MapProgress.GetSelectableNodes(map));
        }

        [Test]
        public void Progress_SurvivesSerializationRoundTrip()
        {
            MapState map = NewMap();
            MapProgress.Visit(map, MapProgress.GetSelectableNodes(map)[0].point);
            MapProgress.Visit(map, MapProgress.GetSelectableNodes(map)[0].point);

            MapState restored = MapState.FromJson(map.ToJson());

            Assert.AreEqual(MapProgress.GetCurrentNode(map).point, MapProgress.GetCurrentNode(restored).point);
            Assert.That(
                MapProgress.GetSelectableNodes(restored).Select(n => n.point),
                Is.EquivalentTo(MapProgress.GetSelectableNodes(map).Select(n => n.point)));
        }
    }
}
