using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Events;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 이벤트 추첨 검증 (§5.4): 동일 런 내 중복 방지, 전부 소진 시 반복 허용.
    /// </summary>
    public class EventSelectorTests
    {
        private static List<EventData> Pool(int count) =>
            Enumerable.Range(0, count).Select(i => new EventData { id = $"evt_{i}" }).ToList();

        [Test]
        public void SelectRandom_NoneVisited_ReturnsFromPool()
        {
            List<EventData> pool = Pool(3);
            EventData result = EventSelector.SelectRandom(pool, new HashSet<string>(), new Random(1));

            Assert.IsTrue(pool.Any(e => e.id == result.id));
        }

        [Test]
        public void SelectRandom_ExcludesVisitedIds()
        {
            List<EventData> pool = Pool(3);
            var visited = new HashSet<string> { "evt_0", "evt_1" };

            for (int seed = 0; seed < 20; seed++)
            {
                EventData result = EventSelector.SelectRandom(pool, visited, new Random(seed));
                Assert.AreEqual("evt_2", result.id, $"seed {seed}: 미방문 이벤트만 나와야 함");
            }
        }

        [Test]
        public void SelectRandom_AllVisited_FallsBackToFullPool()
        {
            List<EventData> pool = Pool(3);
            var visited = new HashSet<string> { "evt_0", "evt_1", "evt_2" };

            EventData result = EventSelector.SelectRandom(pool, visited, new Random(1));

            Assert.IsTrue(pool.Any(e => e.id == result.id), "전부 소진 시 전체 풀에서 재추첨 허용");
        }

        [Test]
        public void SelectRandom_EmptyPool_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => EventSelector.SelectRandom(new List<EventData>(), new HashSet<string>(), new Random(1)));
        }

        [Test]
        public void SelectRandom_NullArguments_Throw()
        {
            List<EventData> pool = Pool(1);
            Assert.Throws<ArgumentNullException>(() => EventSelector.SelectRandom(null, new HashSet<string>(), new Random(1)));
            Assert.Throws<ArgumentNullException>(() => EventSelector.SelectRandom(pool, null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => EventSelector.SelectRandom(pool, new HashSet<string>(), null));
        }
    }
}
