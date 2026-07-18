using System;
using System.Collections.Generic;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Events
{
    /// <summary>선택지 확정 시 보상을 RunState에 적용 (§5.4, §4-20).</summary>
    public static class EventRewardApplier
    {
        /// <summary>
        /// 보상을 적용하고, 상한 등의 이유로 지급하지 못한 보상 목록을 반환한다 (§4-7 — 군대 보유 상한).
        /// 호출자는 반환값이 비어있지 않으면 사용자에게 실패를 안내해야 한다.
        /// </summary>
        public static IReadOnlyList<RewardGrant> Apply(RunState run, IReadOnlyList<RewardGrant> rewards, int maxArmyCount)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (rewards == null) throw new ArgumentNullException(nameof(rewards));
            if (maxArmyCount < 1)
                throw new ArgumentException($"maxArmyCount는 1 이상이어야 합니다. 현재: {maxArmyCount}", nameof(maxArmyCount));

            var skipped = new List<RewardGrant>();

            foreach (RewardGrant reward in rewards)
            {
                switch (reward.type)
                {
                    case RewardType.Army:
                        if (run.armies.Count >= maxArmyCount)
                        {
                            skipped.Add(reward); // 슬롯 상한 도달 — 지급 불가 (§4-7)
                            break;
                        }
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

            return skipped;
        }
    }
}
