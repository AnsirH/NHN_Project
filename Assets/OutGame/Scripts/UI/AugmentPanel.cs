using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Augments;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;

namespace OutGame.UI
{
    /// <summary>
    /// 증강 방 패널 (§5.6, §4-27): pool에서 3개를 무작위 노출 → 선택 시 즉시 런 전체 적용 → 결과 표시 → 복귀.
    /// 선택→적용→결과→계속 흐름 자체는 ChoicePanelBase가 담당(EventPanel과 공유) — 여기서는
    /// AugmentPoolService.PickRandomThree()로 매 방문마다 새로 뽑는 pool과 AugmentApplier 연동만 다룬다.
    /// </summary>
    public class AugmentPanel : ChoicePanelBase
    {
        private RunState run;

        public void Open(RunState runState, IReadOnlyList<AugmentDefinition> pool, System.Random rng)
        {
            if (runState == null) throw new ArgumentNullException(nameof(runState));
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            run = runState;

            ResetChoiceUI();

            List<AugmentData> poolData = pool.Select(p => p.ToData()).ToList(); // 콘텐츠 결함은 여기서 즉시 드러남
            List<AugmentData> options = AugmentPoolService.PickRandomThree(poolData, rng);

            foreach (AugmentData option in options)
                SpawnChoiceButton($"{option.displayName}\n{option.description}", () => OnAugmentSelected(option));

            gameObject.SetActive(true);
        }

        private void OnAugmentSelected(AugmentData augment)
        {
            AugmentApplier.Apply(run, augment);
            ShowResult($"'{augment.displayName}'를 선택했습니다.");
        }
    }
}
