using System;
using OutGame.Logic.Maps;

namespace OutGame.Logic.Runs
{
    /// <summary>
    /// 런 생성 (§5.2): 맵 확정 시 RunState 생성 + 기본 군대 지급.
    /// </summary>
    public static class RunStateFactory
    {
        public static RunState Create(MapState map, RunConfig config)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();

            var run = new RunState
            {
                mapState = map,
                gold = config.startingGold,
            };

            for (int i = 0; i < config.startingArmyCount; i++)
            {
                run.armies.Add(new ArmyInstance
                {
                    instanceId = Guid.NewGuid().ToString(),
                    armyDefId = config.startingArmyDefId,
                });
            }

            return run;
        }
    }
}
