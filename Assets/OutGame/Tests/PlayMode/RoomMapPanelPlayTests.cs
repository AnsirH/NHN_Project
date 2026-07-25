using System.Collections;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;
using OutGame.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// RoomMapPanel 프리팹 스모크 테스트 — 프리팹은 SceneSetupM2.Run()으로 생성돼 있어야 한다.
    /// </summary>
    public class RoomMapPanelPlayTests
    {
        private GameObject canvasGo;
        private RoomMapPanel panel;
        private MapState map;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/RoomMapPanel");
            Assert.IsNotNull(prefab, "RoomMapPanel 프리팹이 Resources/OutGame/에 없음 — SceneSetupM2.Run() 실행 필요");

            panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<RoomMapPanel>();
            Assert.IsNotNull(panel);

            map = new MapGenerator(new MapGenerationConfig(), seed: 42).Generate();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
        }

        [UnityTest]
        public IEnumerator Open_BuildsViewForEveryNode()
        {
            panel.Open(map);
            yield return null;

            Assert.AreEqual(map.nodes.Count, panel.NodeViews.Count);
            Assert.That(
                panel.NodeViews.Select(v => v.Node.point),
                Is.EquivalentTo(map.nodes.Select(n => n.point)));
        }

        [UnityTest]
        public IEnumerator Open_OnlyFirstFloorIsSelectable()
        {
            panel.Open(map);
            yield return null;

            foreach (RoomNodeView view in panel.NodeViews)
            {
                bool expectSelectable = view.Node.point.y == 0;
                Assert.AreEqual(expectSelectable, view.Button.interactable,
                    $"{view.Node.point} 노드의 선택 가능 상태가 규칙과 다름");
                Assert.AreEqual(
                    expectSelectable ? RoomNodeView.NodeState.Selectable : RoomNodeView.NodeState.Locked,
                    view.State);
            }
        }

        [UnityTest]
        public IEnumerator ClickSelectableNode_RaisesRoomSelected()
        {
            panel.Open(map);
            yield return null;

            MapNode received = null;
            panel.RoomSelected += n => received = n;

            RoomNodeView firstFloorView = panel.NodeViews.First(v => v.Node.point.y == 0);
            firstFloorView.Button.onClick.Invoke();

            Assert.IsNotNull(received, "선택 가능 노드 클릭 시 RoomSelected가 발행돼야 함");
            Assert.AreEqual(firstFloorView.Node.point, received.point);
        }

        [UnityTest]
        public IEnumerator ClickLockedNode_DoesNotRaiseRoomSelected()
        {
            panel.Open(map);
            yield return null;

            MapNode received = null;
            panel.RoomSelected += n => received = n;

            RoomNodeView lockedView = panel.NodeViews.First(v => v.Node.point.y > 0);
            lockedView.Button.onClick.Invoke(); // interactable 우회 호출이어도 상태 가드로 막혀야 함

            Assert.IsNull(received, "잠긴 노드는 클릭돼도 RoomSelected가 발행되면 안 됨");
        }

        [UnityTest]
        public IEnumerator VisitAndRefresh_UpdatesNodeStates()
        {
            panel.Open(map);
            yield return null;

            GridPoint start = panel.NodeViews.First(v => v.Node.point.y == 0).Node.point;
            MapProgress.Visit(map, start);
            panel.Refresh();
            yield return null;

            RoomNodeView currentView = panel.NodeViews.First(v => v.Node.point.Equals(start));
            Assert.AreEqual(RoomNodeView.NodeState.Current, currentView.State);

            MapNode startNode = map.GetNode(start);
            foreach (RoomNodeView view in panel.NodeViews)
            {
                if (view.Node.point.Equals(start)) continue;
                bool expectSelectable = startNode.outgoing.Contains(view.Node.point);
                Assert.AreEqual(expectSelectable, view.State == RoomNodeView.NodeState.Selectable,
                    $"{view.Node.point} 상태 불일치 (방문 후)");
            }
        }

        [UnityTest]
        public IEnumerator Open_BuildsLegendWithFivePrimaryTypes()
        {
            panel.Open(map);
            yield return null;

            Transform legend = panel.transform.Find("Legend");
            Assert.IsNotNull(legend, "Legend 컨테이너가 프리팹에 있어야 함");
            Assert.AreEqual(5, legend.childCount, "범례는 1차 방 타입 5종을 표시 (2026-07-19 증강 추가, §4-27)");
        }
    }
}
