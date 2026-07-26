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

        [Test]
        public void SetPendingBattle_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => BattleBridge.SetPendingBattle(null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => BattleBridge.SetPendingBattle(NewSetup(), null));
        }

        [Test]
        public void ConsumePendingSetup_AfterSetPendingBattle_ReturnsSameInstance()
        {
            BattleSetupData sent = NewSetup();
            BattleBridge.SetPendingBattle(sent, _ => { });

            Assert.AreSame(sent, BattleBridge.ConsumePendingSetup());
        }

        [Test]
        public void ConsumePendingSetup_CalledTwice_SecondCallReturnsNull()
        {
            BattleBridge.SetPendingBattle(NewSetup(), _ => { });

            BattleBridge.ConsumePendingSetup();
            Assert.IsNull(BattleBridge.ConsumePendingSetup(), "한 번 꺼내가면 비워져야 함 — 다음 전투와 섞이면 안 됨");
        }

        [Test]
        public void ConsumePendingSetup_WithoutSetPendingBattle_ReturnsNull()
        {
            Assert.IsNull(BattleBridge.ConsumePendingSetup());
        }

        [Test]
        public void CompleteBattle_InvokesStoredCallback()
        {
            BattleResultData received = null;
            BattleBridge.SetPendingBattle(NewSetup(), result => received = result);

            var sentResult = new BattleResultData { roomId = "room_2_1", victory = true };
            BattleBridge.CompleteBattle(sentResult);

            Assert.AreSame(sentResult, received);
        }

        [Test]
        public void CompleteBattle_NullResult_Throws()
        {
            BattleBridge.SetPendingBattle(NewSetup(), _ => { });
            Assert.Throws<ArgumentNullException>(() => BattleBridge.CompleteBattle(null));
        }

        [Test]
        public void CompleteBattle_WithoutPendingCallback_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => BattleBridge.CompleteBattle(new BattleResultData { roomId = "room_2_1", victory = true }));
        }

        [Test]
        public void CompleteBattle_CalledTwice_SecondCallThrows()
        {
            BattleBridge.SetPendingBattle(NewSetup(), _ => { });
            var result = new BattleResultData { roomId = "room_2_1", victory = true };

            BattleBridge.CompleteBattle(result);
            Assert.Throws<InvalidOperationException>(() => BattleBridge.CompleteBattle(result),
                "콜백을 소비한 뒤 다시 호출하면 대기 상태가 없어야 함");
        }

        [Test]
        public void ResetToDefault_ClearsPendingBattleState()
        {
            BattleBridge.SetPendingBattle(NewSetup(), _ => { });

            BattleBridge.ResetToDefault();

            Assert.IsNull(BattleBridge.ConsumePendingSetup());
            Assert.Throws<InvalidOperationException>(
                () => BattleBridge.CompleteBattle(new BattleResultData { roomId = "room_2_1", victory = true }));
        }
    }
}
