using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// 난이도 커브 기준점(RunState.powerRoomsVisited, 2026-07-26 §4-28 재설계) 증가 배선 검증 —
    /// 증원/증강/이벤트 방 입장 시에만 오르고, 전투방은 오르지 않아야 한다(전투 승리 드롭은
    /// 확률적이라 카운터에서 제외하기로 확정).
    /// </summary>
    public class InGameFlowControllerPowerCounterPlayTests
    {
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(SceneNames.InGame);
        }

        // 노드 하나짜리 최소 맵 — OnRoomSelected의 카운터 증가는 방 타입만으로 분기하므로
        // 맵 그래프의 나머지 구조(전투/보스 등 필수 배치 규칙)는 이 테스트의 관심사가 아니다.
        private static RunState CreateSingleRoomRun(RoomType roomType)
        {
            var map = new MapState
            {
                nodes = new List<MapNode> { new MapNode(roomType, new GridPoint(0, 0)) },
            };
            return RunStateFactory.Create(map, new RunConfig());
        }

        private static IEnumerator SelectSingleRoomAndGetRun(RoomType roomType, System.Action<RunState> onReady)
        {
            RunSessionContext.SetPendingRun(CreateSingleRoomRun(roomType));
            yield return SceneManager.LoadSceneAsync(SceneNames.InGame, LoadSceneMode.Additive);
            yield return null;

            var flow = GameObject.Find("InGameFlow").GetComponent<InGameFlowController>();
            var run = (RunState)typeof(InGameFlowController).GetField("run", Priv).GetValue(flow);
            MapNode node = run.mapState.nodes[0];

            typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv).Invoke(flow, new object[] { node });
            yield return null;

            onReady(run);
        }

        [UnityTest]
        public IEnumerator EventRoomSelected_IncrementsPowerRoomsVisited()
        {
            RunState observed = null;
            yield return SelectSingleRoomAndGetRun(RoomType.Event, run => observed = run);
            Assert.AreEqual(1, observed.powerRoomsVisited);
        }

        [UnityTest]
        public IEnumerator RestRoomSelected_IncrementsPowerRoomsVisited()
        {
            RunState observed = null;
            yield return SelectSingleRoomAndGetRun(RoomType.Rest, run => observed = run);
            Assert.AreEqual(1, observed.powerRoomsVisited);
        }

        [UnityTest]
        public IEnumerator AugmentRoomSelected_IncrementsPowerRoomsVisited()
        {
            RunState observed = null;
            yield return SelectSingleRoomAndGetRun(RoomType.Augment, run => observed = run);
            Assert.AreEqual(1, observed.powerRoomsVisited);
        }

        [UnityTest]
        public IEnumerator BattleRoomSelected_DoesNotIncrementPowerRoomsVisited()
        {
            RunState observed = null;
            yield return SelectSingleRoomAndGetRun(RoomType.NormalBattle, run => observed = run);
            Assert.AreEqual(0, observed.powerRoomsVisited,
                "전투 승리 드롭은 확률적이라 난이도 커브 카운터에서 제외해야 함(§4-28)");
        }

        [UnityTest]
        public IEnumerator TwoPowerRoomsInSequence_AccumulatesAcrossVisits()
        {
            // 런 전체에 걸쳐 누적되는 카운터라 단발성 0→1뿐 아니라 1→2 증가도 검증해야 한다.
            var first = new MapNode(RoomType.Event, new GridPoint(0, 0));
            var second = new MapNode(RoomType.Augment, new GridPoint(0, 1));
            first.AddOutgoing(second.point);
            second.AddIncoming(first.point);
            var map = new MapState { nodes = new List<MapNode> { first, second } };
            var run = RunStateFactory.Create(map, new RunConfig());

            RunSessionContext.SetPendingRun(run);
            yield return SceneManager.LoadSceneAsync(SceneNames.InGame, LoadSceneMode.Additive);
            yield return null;

            var flow = GameObject.Find("InGameFlow").GetComponent<InGameFlowController>();
            var liveRun = (RunState)typeof(InGameFlowController).GetField("run", Priv).GetValue(flow);
            MethodInfo onRoomSelected = typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv);

            onRoomSelected.Invoke(flow, new object[] { first });
            yield return null;
            Assert.AreEqual(1, liveRun.powerRoomsVisited, "첫 번째 파워룸 통과 후 1이어야 함");

            onRoomSelected.Invoke(flow, new object[] { second });
            yield return null;
            Assert.AreEqual(2, liveRun.powerRoomsVisited, "두 번째 파워룸까지 통과하면 누적 2여야 함");
        }
    }
}
