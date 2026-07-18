using System;
using System.Collections;
using NUnit.Framework;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;
using OutGame.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// DummyBattlePanel 프리팹 스모크 테스트 — 프리팹은 SceneSetupM6UI.Run()으로 생성돼 있어야 한다.
    /// BattleBridge.Implementation에 그대로 대입되는 시그니처(Open)를 검증한다.
    /// </summary>
    public class DummyBattlePanelPlayTests
    {
        private GameObject canvasGo;
        private DummyBattlePanel panel;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/DummyBattlePanel");
            Assert.IsNotNull(prefab, "DummyBattlePanel 프리팹 없음 — SceneSetupM6UI.Run() 실행 필요");
            panel = UnityEngine.Object.Instantiate(prefab, canvasGo.transform).GetComponent<DummyBattlePanel>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.Destroy(canvasGo);

        private static BattleSetupData NewSetup() => new BattleSetupData
        {
            roomId = "room_3_1",
            roomType = RoomType.NormalBattle,
            encounterId = "enc_default",
        };

        [Test]
        public void Open_NullSetup_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => panel.Open(null, _ => { }));
        }

        [Test]
        public void Open_NullCallback_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => panel.Open(NewSetup(), null));
        }

        [UnityTest]
        public IEnumerator VictoryButton_InvokesCallbackWithVictoryTrue()
        {
            BattleResultData received = null;
            BattleSetupData setup = NewSetup();
            panel.Open(setup, result => received = result);
            yield return null;

            panel.transform.Find("Window/VictoryButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsNotNull(received);
            Assert.IsTrue(received.victory);
            Assert.AreEqual(setup.roomId, received.roomId);
            Assert.IsFalse(panel.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator DefeatButton_InvokesCallbackWithVictoryFalse()
        {
            BattleResultData received = null;
            panel.Open(NewSetup(), result => received = result);
            yield return null;

            panel.transform.Find("Window/DefeatButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsNotNull(received);
            Assert.IsFalse(received.victory);
        }
    }
}
