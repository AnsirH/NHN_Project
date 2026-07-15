using System;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// MapState JSON 직렬화 왕복 검증 — 런 저장/이어하기(상세 기획 §6)의 전제 조건.
    /// </summary>
    public class MapStateSerializationTests
    {
        [Test]
        public void ToJson_FromJson_RoundTrip_PreservesGraph()
        {
            MapState original = new MapGenerator(new MapGenerationConfig(), seed: 42).Generate();
            original.visitedPath.Add(new GridPoint(1, 0));
            original.visitedPath.Add(new GridPoint(2, 1));

            MapState restored = MapState.FromJson(original.ToJson());

            Assert.AreEqual(original.configName, restored.configName);
            Assert.AreEqual(original.seed, restored.seed);
            Assert.AreEqual(original.visitedPath, restored.visitedPath);
            Assert.AreEqual(original.nodes.Count, restored.nodes.Count);

            foreach (MapNode expected in original.nodes)
            {
                MapNode actual = restored.GetNode(expected.point);
                Assert.IsNotNull(actual, $"복원된 맵에 {expected.point} 노드 없음");
                Assert.AreEqual(expected.id, actual.id);
                Assert.AreEqual(expected.roomType, actual.roomType);
                Assert.AreEqual(expected.incoming, actual.incoming);
                Assert.AreEqual(expected.outgoing, actual.outgoing);
                Assert.AreEqual(expected.posX, actual.posX, 1e-5f);
                Assert.AreEqual(expected.posY, actual.posY, 1e-5f);
            }
        }

        [Test]
        public void FromJson_EmptyString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => MapState.FromJson(""));
            Assert.Throws<ArgumentException>(() => MapState.FromJson("   "));
        }

        [Test]
        public void FromJson_MalformedJson_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => MapState.FromJson("not json"));
        }

        [Test]
        public void FromJson_EmptyObject_ThrowsArgumentException()
        {
            // 노드가 하나도 없는 저장 데이터는 손상으로 취급한다
            Assert.Throws<ArgumentException>(() => MapState.FromJson("{}"));
        }

        [Test]
        public void GetBossNode_AfterRoundTrip_ReturnsBoss()
        {
            MapState original = new MapGenerator(new MapGenerationConfig(), seed: 11).Generate();
            MapState restored = MapState.FromJson(original.ToJson());

            MapNode boss = restored.GetBossNode();
            Assert.IsNotNull(boss);
            Assert.AreEqual(original.GetBossNode().point, boss.point);
        }
    }
}
