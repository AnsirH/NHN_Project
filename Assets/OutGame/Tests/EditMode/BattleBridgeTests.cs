using System;
using NUnit.Framework;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// BattleBridge — 아웃게임→인게임 호출 계약 (§7.3). Implementation은 스왑 가능해야 한다
    /// (M6에서는 더미 UI, 추후 실제 인게임 코드로 교체).
    /// </summary>
    public class BattleBridgeTests
    {
        [TearDown]
        public void TearDown() => BattleBridge.ResetToDefault(); // 테스트 간 정적 상태 오염 방지

        private static BattleSetupData NewSetup() => new BattleSetupData
        {
            roomId = "room_2_1",
            roomType = RoomType.NormalBattle,
            encounterId = "enc_default",
        };

        [Test]
        public void StartBattle_NullSetup_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BattleBridge.StartBattle(null, _ => { }));
        }

        [Test]
        public void StartBattle_NullCallback_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BattleBridge.StartBattle(NewSetup(), null));
        }

        [Test]
        public void StartBattle_DelegatesToImplementation()
        {
            BattleSetupData received = null;
            BattleBridge.Implementation = (setup, onResult) => received = setup;

            BattleSetupData sent = NewSetup();
            BattleBridge.StartBattle(sent, _ => { });

            Assert.AreSame(sent, received);
        }

        [Test]
        public void StartBattle_DefaultImplementation_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => BattleBridge.StartBattle(NewSetup(), _ => { }));
        }

        [Test]
        public void Implementation_SetNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BattleBridge.Implementation = null);
        }
    }
}
