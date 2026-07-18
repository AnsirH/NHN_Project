using System;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Battle
{
    /// <summary>전투 승리 보상 적용 (§4-20/§9 — 1차는 재화만, 수치는 밸런스 튜닝 전 초안).</summary>
    public static class BattleRewardApplier
    {
        public static void ApplyVictoryReward(RunState run, int goldAmount)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (goldAmount < 0) throw new ArgumentException("goldAmount는 0 이상이어야 합니다.", nameof(goldAmount));

            run.gold += goldAmount;
        }
    }
}
