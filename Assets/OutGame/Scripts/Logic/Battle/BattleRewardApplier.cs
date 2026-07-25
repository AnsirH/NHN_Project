using System;
using System.Collections.Generic;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Battle
{
    /// <summary>전투 승리 보상 적용 (§4-20/§9: 재화, §4-28: 병과 기반 아이템 드롭).</summary>
    public static class BattleRewardApplier
    {
        public static void ApplyVictoryReward(RunState run, int goldAmount)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (goldAmount < 0) throw new ArgumentException("goldAmount는 0 이상이어야 합니다.", nameof(goldAmount));

            run.gold += goldAmount;
        }

        /// <summary>드롭된 아이템을 보유 목록에 추가한다 (§4-28) — 이벤트 아이템 보상과 동일한 부여 방식.</summary>
        public static void ApplyItemDrops(RunState run, IReadOnlyList<string> droppedItemIds)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (droppedItemIds == null) throw new ArgumentNullException(nameof(droppedItemIds));

            foreach (string itemId in droppedItemIds)
                run.ownedItemIds.Add(itemId); // 부대 단위 부여이므로 중복 보유 허용(EventRewardApplier와 동일 근거)
        }
    }
}
