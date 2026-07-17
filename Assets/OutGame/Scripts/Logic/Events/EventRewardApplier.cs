using System;
using System.Collections.Generic;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Events
{
    /// <summary>선택지 확정 시 보상을 RunState에 적용 (§5.4, §4-20).</summary>
    public static class EventRewardApplier
    {
        public static void Apply(RunState run, IReadOnlyList<RewardGrant> rewards)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (rewards == null) throw new ArgumentNullException(nameof(rewards));

            foreach (RewardGrant reward in rewards)
            {
                switch (reward.type)
                {
                    case RewardType.Army:
                        run.armies.Add(new Runs.ArmyInstance
                        {
                            instanceId = Guid.NewGuid().ToString(),
                            armyDefId = reward.armyDefId,
                        });
                        break;
                    case RewardType.Item:
                        run.ownedItemIds.Add(reward.itemId); // 부대 단위 부여이므로 중복 보유 허용
                        break;
                    case RewardType.Gold:
                        run.gold += reward.goldAmount;
                        break;
                    case RewardType.None:
                    default:
                        break;
                }
            }
        }
    }
}
