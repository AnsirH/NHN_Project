using System;
using NUnit.Framework;
using OutGame.Logic.Augments;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>증강 적용 검증 (§4-27): 런 전체 적용이므로 선택 이력만 누적하면 된다. 중복 선택 허용.</summary>
    public class AugmentApplierTests
    {
        private RunState run;

        [SetUp]
        public void SetUp()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            run = RunStateFactory.Create(map, new RunConfig());
        }

        [Test]
        public void Apply_AddsAugmentIdToRunState()
        {
            AugmentApplier.Apply(run, new AugmentData { id = "aug_archer_atk" });

            CollectionAssert.Contains(run.selectedAugmentIds, "aug_archer_atk");
        }

        [Test]
        public void Apply_SameAugmentTwice_StacksAsTwoEntries()
        {
            var augment = new AugmentData { id = "aug_archer_atk" };
            AugmentApplier.Apply(run, augment);
            AugmentApplier.Apply(run, augment);

            Assert.AreEqual(2, run.selectedAugmentIds.FindAll(id => id == "aug_archer_atk").Count,
                "중복 선택 허용/스택 — pool에서 제외하지 않음(사용자 확정)");
        }

        [Test]
        public void Apply_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => AugmentApplier.Apply(null, new AugmentData { id = "x" }));
            Assert.Throws<ArgumentNullException>(() => AugmentApplier.Apply(run, null));
        }
    }
}
