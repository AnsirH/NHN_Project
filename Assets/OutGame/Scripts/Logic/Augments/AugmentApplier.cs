using System;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Augments
{
    /// <summary>증강 선택 적용 (§4-27) — 런 전체 적용이므로 선택 이력만 기록하면 된다. 중복 선택/스택 허용.</summary>
    public static class AugmentApplier
    {
        public static void Apply(RunState run, AugmentData augment)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (augment == null) throw new ArgumentNullException(nameof(augment));

            run.selectedAugmentIds.Add(augment.id);
        }
    }
}
