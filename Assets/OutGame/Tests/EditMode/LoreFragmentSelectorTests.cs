using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Narrative;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// "의지의 파편" 추첨 검증 (§2-2 세계관: 아주 드묾) — 낮은 확률로만 노출, 풀이 비어 있으면
    /// 항상 미노출, null 인자는 예외.
    /// </summary>
    public class LoreFragmentSelectorTests
    {
        private static List<LoreFragmentData> Pool(int count) =>
            Enumerable.Range(0, count).Select(i => new LoreFragmentData { id = $"frag_{i}", text = $"text {i}" }).ToList();

        [Test]
        public void TrySelect_ChanceZero_NeverTriggers()
        {
            List<LoreFragmentData> pool = Pool(3);
            for (int seed = 0; seed < 20; seed++)
            {
                bool triggered = LoreFragmentSelector.TrySelect(pool, new Random(seed), 0f, out LoreFragmentData selected);
                Assert.IsFalse(triggered, $"seed {seed}: 확률 0이면 절대 노출되면 안 됨");
                Assert.IsNull(selected);
            }
        }

        [Test]
        public void TrySelect_ChanceOne_AlwaysTriggersFromPool()
        {
            List<LoreFragmentData> pool = Pool(3);
            for (int seed = 0; seed < 20; seed++)
            {
                bool triggered = LoreFragmentSelector.TrySelect(pool, new Random(seed), 1f, out LoreFragmentData selected);
                Assert.IsTrue(triggered, $"seed {seed}: 확률 1이면 항상 노출돼야 함");
                Assert.IsTrue(pool.Any(f => f.id == selected.id));
            }
        }

        [Test]
        public void TrySelect_EmptyPool_NeverTriggersEvenWithChanceOne()
        {
            bool triggered = LoreFragmentSelector.TrySelect(
                new List<LoreFragmentData>(), new Random(1), 1f, out LoreFragmentData selected);

            Assert.IsFalse(triggered);
            Assert.IsNull(selected);
        }

        [Test]
        public void TrySelect_NullArguments_Throw()
        {
            List<LoreFragmentData> pool = Pool(1);
            Assert.Throws<ArgumentNullException>(() =>
                LoreFragmentSelector.TrySelect(null, new Random(1), 0.5f, out _));
            Assert.Throws<ArgumentNullException>(() =>
                LoreFragmentSelector.TrySelect(pool, null, 0.5f, out _));
        }
    }
}
