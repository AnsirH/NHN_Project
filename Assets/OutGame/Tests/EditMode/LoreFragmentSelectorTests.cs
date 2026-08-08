using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Narrative;

namespace OutGame.Tests.EditMode
{
    /// <summary>"의지의 파편" 확률 판정(LoreFragmentSelector) 검증 — 결정성 확보를 위해 확률을
    /// 0/1로 고정하거나 시드를 고정한 Random을 주입한다(설계 문서 §7 재구현 시 반영 사항).</summary>
    public class LoreFragmentSelectorTests
    {
        private static List<LoreFragmentData> Pool(int count) =>
            new List<LoreFragmentData>(BuildPool(count));

        private static IEnumerable<LoreFragmentData> BuildPool(int count)
        {
            for (int i = 0; i < count; i++)
                yield return new LoreFragmentData { id = $"fragment_{i}", text = $"text_{i}" };
        }

        [Test]
        public void TrySelect_ChanceZero_NeverSelects()
        {
            var pool = Pool(3);
            var rng = new Random(1);

            bool result = LoreFragmentSelector.TrySelect(pool, rng, triggerChance: 0f, out LoreFragmentData selected);

            Assert.IsFalse(result);
            Assert.IsNull(selected);
        }

        [Test]
        public void TrySelect_ChanceOne_AlwaysSelectsFromPool()
        {
            var pool = Pool(3);
            var rng = new Random(1);

            bool result = LoreFragmentSelector.TrySelect(pool, rng, triggerChance: 1f, out LoreFragmentData selected);

            Assert.IsTrue(result);
            Assert.IsNotNull(selected);
            CollectionAssert.Contains(pool, selected);
        }

        [Test]
        public void TrySelect_EmptyPool_NeverSelectsEvenAtChanceOne()
        {
            var pool = new List<LoreFragmentData>();
            var rng = new Random(1);

            bool result = LoreFragmentSelector.TrySelect(pool, rng, triggerChance: 1f, out LoreFragmentData selected);

            Assert.IsFalse(result);
            Assert.IsNull(selected);
        }

        [Test]
        public void TrySelect_DoesNotTrackVisitedIds_CanRepeatAcrossCalls()
        {
            // EventSelector와 달리 반복 방지 로직이 없다는 설계 의도(확률 자체가 낮아 불필요) —
            // 같은 rng 시퀀스로 여러 번 호출해도 예외 없이 계속 뽑힐 수 있어야 한다.
            var pool = Pool(1);
            var rng = new Random(1);

            for (int i = 0; i < 5; i++)
            {
                bool result = LoreFragmentSelector.TrySelect(pool, rng, triggerChance: 1f, out LoreFragmentData selected);
                Assert.IsTrue(result);
                Assert.AreEqual(pool[0], selected);
            }
        }

        [Test]
        public void TrySelect_NullPool_Throws()
        {
            var rng = new Random(1);

            Assert.Throws<ArgumentNullException>(() =>
                LoreFragmentSelector.TrySelect(null, rng, 0.5f, out _));
        }

        [Test]
        public void TrySelect_NullRng_Throws()
        {
            var pool = Pool(1);

            Assert.Throws<ArgumentNullException>(() =>
                LoreFragmentSelector.TrySelect(pool, null, 0.5f, out _));
        }
    }
}
