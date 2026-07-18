using System;

namespace OutGame.Logic.Runs
{
    /// <summary>배치 슬롯 진형 저장용 (§5.7) — RunState.deployment에 담겨 방을 넘어가도 유지된다.</summary>
    [Serializable]
    public class ArmySlotAssignment
    {
        public string armyInstanceId;
        public int slotId;
    }
}
