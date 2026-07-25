using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Augments;

namespace OutGame.Tests.EditMode
{
    /// <summary>증강 pool 추출 검증 (§4-27): 3개 무작위 노출, 같은 화면 안에서는 중복 없음.</summary>
    public class AugmentPoolServiceTests
    {
        private static List<AugmentData> NineItemPool() =>
            Enumerable.Range(0, 9).Select(i => new AugmentData { id = $"aug_{i}" }).ToList();

        [Test]
        public void PickRandomThree_ReturnsThreeDistinctItems()
        {
            List<AugmentData> picked = AugmentPoolService.PickRandomThree(NineItemPool(), new Random(1));

            Assert.AreEqual(3, picked.Count);
            Assert.AreEqual(3, picked.Select(a => a.id).Distinct().Count(), "한 화면 안에서는 중복 노출 금지");
        }

        [Test]
        public void PickRandomThree_AllFromPool()
        {
            List<AugmentData> pool = NineItemPool();
            List<AugmentData> picked = AugmentPoolService.PickRandomThree(pool, new Random(7));

            foreach (AugmentData a in picked)
                CollectionAssert.Contains(pool.Select(p => p.id).ToList(), a.id);
        }

        [Test]
        public void PickRandomThree_DifferentSeedsProduceDifferentResults()
        {
            List<AugmentData> pool = NineItemPool();
            var picked1 = AugmentPoolService.PickRandomThree(pool, new Random(1)).Select(a => a.id).ToList();
            var picked2 = AugmentPoolService.PickRandomThree(pool, new Random(2)).Select(a => a.id).ToList();

            CollectionAssert.AreNotEqual(picked1, picked2, "시드가 다르면 결과도 달라야 함(결정적 셔플 확인)");
        }

        [Test]
        public void PickRandomThree_PoolSmallerThanThree_ReturnsAll()
        {
            List<AugmentData> pool = new List<AugmentData> { new AugmentData { id = "a" }, new AugmentData { id = "b" } };
            List<AugmentData> picked = AugmentPoolService.PickRandomThree(pool, new Random(1));

            Assert.AreEqual(2, picked.Count);
        }

        [Test]
        public void PickRandomThree_EmptyPool_Throws()
        {
            Assert.Throws<ArgumentException>(() => AugmentPoolService.PickRandomThree(new List<AugmentData>(), new Random(1)));
        }

        [Test]
        public void PickRandomThree_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => AugmentPoolService.PickRandomThree(null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => AugmentPoolService.PickRandomThree(NineItemPool(), null));
        }
    }
}
