using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Maps;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 방 그래프 UI (상세 기획 §5.3). 씬 독립 프리팹 — 어떤 Canvas 아래든 붙여서 사용.
    /// 사용법: Open(MapState) → 노드 선택 시 RoomSelected 발행 → 방 처리 후 Refresh() 호출.
    /// 방문 확정(MapProgress.Visit)은 이 패널의 책임이 아니다 (플로우 쪽 결정).
    /// </summary>
    public class RoomMapPanel : MonoBehaviour
    {
        [Header("구조 참조")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform lineLayer;
        [SerializeField] private RectTransform nodeLayer;
        [SerializeField] private RectTransform legendContainer;

        [Header("표시 설정")]
        [SerializeField] private RoomTypeVisualSet visuals;
        [SerializeField] private float nodeSize = 64f;
        [SerializeField] private float unitScale = 110f;   // MapNode.posX/posY(논리 단위) → 픽셀
        [SerializeField] private float contentPadding = 90f;
        [SerializeField] private float lineThickness = 4f;

        private static readonly Color LineDimColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color LineTraveledColor = new Color(1f, 0.85f, 0.2f, 0.85f);

        private readonly List<RoomNodeView> nodeViews = new List<RoomNodeView>();
        private readonly List<(Image image, GridPoint from, GridPoint to)> lines
            = new List<(Image, GridPoint, GridPoint)>();

        public MapState Map { get; private set; }
        public IReadOnlyList<RoomNodeView> NodeViews => nodeViews;

        /// <summary>선택 가능한 노드가 클릭됐을 때 발행. 방문 확정은 구독자 책임.</summary>
        public event Action<MapNode> RoomSelected;

        public void Open(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (map.nodes == null || map.nodes.Count == 0)
                throw new ArgumentException("노드가 없는 MapState는 표시할 수 없습니다.", nameof(map));
            ValidateWiring();

            Map = map;
            gameObject.SetActive(true);
            Rebuild();
        }

        private void ValidateWiring()
        {
            if (content == null || lineLayer == null || nodeLayer == null)
                throw new InvalidOperationException(
                    "RoomMapPanel의 content/lineLayer/nodeLayer가 배선되지 않았습니다 — 프리팹 구성(SceneSetupM2) 확인");
            if (visuals == null)
                throw new InvalidOperationException(
                    "RoomMapPanel.visuals(RoomTypeVisualSet)가 할당되지 않았습니다");
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>방문 기록이 바뀐 뒤(MapProgress.Visit 후) 호출 — 노드 상태와 경로 강조를 갱신한다.</summary>
        public void Refresh()
        {
            if (Map == null) return;

            HashSet<GridPoint> visited = new HashSet<GridPoint>(Map.visitedPath);
            GridPoint? current = Map.visitedPath.Count > 0
                ? Map.visitedPath[Map.visitedPath.Count - 1]
                : (GridPoint?)null;
            HashSet<GridPoint> selectable = new HashSet<GridPoint>(
                MapProgress.GetSelectableNodes(Map).Select(n => n.point));

            foreach (RoomNodeView view in nodeViews)
            {
                if (current.HasValue && view.Node.point.Equals(current.Value))
                    view.SetState(RoomNodeView.NodeState.Current);
                else if (visited.Contains(view.Node.point))
                    view.SetState(RoomNodeView.NodeState.Visited);
                else if (selectable.Contains(view.Node.point))
                    view.SetState(RoomNodeView.NodeState.Selectable);
                else
                    view.SetState(RoomNodeView.NodeState.Locked);
            }

            foreach ((Image image, GridPoint from, GridPoint to) in lines)
                image.color = IsTraveledEdge(from, to) ? LineTraveledColor : LineDimColor;
        }

        private bool IsTraveledEdge(GridPoint from, GridPoint to)
        {
            for (int i = 0; i < Map.visitedPath.Count - 1; i++)
            {
                if (Map.visitedPath[i].Equals(from) && Map.visitedPath[i + 1].Equals(to))
                    return true;
            }
            return false;
        }

        // ── 구성 ─────────────────────────────────────────────────────

        private void Rebuild()
        {
            Clear();

            float minX = Map.nodes.Min(n => n.posX);
            float maxX = Map.nodes.Max(n => n.posX);
            float minY = Map.nodes.Min(n => n.posY);
            float maxY = Map.nodes.Max(n => n.posY);

            float width = (maxX - minX) * unitScale + contentPadding * 2f;
            float height = (maxY - minY) * unitScale + contentPadding * 2f;
            content.sizeDelta = new Vector2(width, height);

            Vector2 ToContentPos(MapNode node) => new Vector2(
                (node.posX - (minX + maxX) / 2f) * unitScale,
                contentPadding + (node.posY - minY) * unitScale);

            Dictionary<GridPoint, Vector2> positions = Map.nodes.ToDictionary(n => n.point, ToContentPos);

            foreach (MapNode node in Map.nodes)
            {
                foreach (GridPoint dest in node.outgoing)
                {
                    if (!positions.TryGetValue(dest, out Vector2 destPos))
                        throw new ArgumentException(
                            $"손상된 맵 데이터 — {node.point}의 연결 대상 {dest} 노드가 없습니다");
                    CreateLine(positions[node.point], destPos, node.point, dest);
                }
            }

            foreach (MapNode node in Map.nodes)
            {
                RoomNodeView view = RoomNodeView.Create(nodeLayer, node, visuals, nodeSize);
                ((RectTransform)view.transform).anchoredPosition = positions[node.point];
                view.Clicked += OnNodeClicked;
                nodeViews.Add(view);
            }

            BuildLegend();
            Refresh();

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases(); // 레이아웃 확정 전에 스크롤 위치를 잡으면 무시될 수 있음
                scrollRect.verticalNormalizedPosition = 0f; // 1층(아래)부터 시작
            }
        }

        private void Clear()
        {
            foreach (RoomNodeView view in nodeViews)
            {
                if (view == null) continue;
                view.Clicked -= OnNodeClicked; // Destroy는 프레임 끝 지연 — 재구성 중 이전 맵 노드의 클릭 유입 차단
                Destroy(view.gameObject);
            }
            nodeViews.Clear();

            foreach ((Image image, _, _) in lines)
                if (image != null) Destroy(image.gameObject);
            lines.Clear();

            if (legendContainer != null)
                for (int i = legendContainer.childCount - 1; i >= 0; i--)
                    Destroy(legendContainer.GetChild(i).gameObject);
        }

        private void CreateLine(Vector2 from, Vector2 to, GridPoint fromPoint, GridPoint toPoint)
        {
            var go = new GameObject($"Line_{fromPoint}_{toPoint}", typeof(RectTransform));
            go.transform.SetParent(lineLayer, false);

            Vector2 delta = to - from;
            float length = Mathf.Max(0f, delta.magnitude - nodeSize); // 노드에 겹치지 않게 축소

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(length, lineThickness);
            rect.anchoredPosition = from + delta.normalized * (nodeSize / 2f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var image = go.AddComponent<Image>();
            image.color = LineDimColor;
            image.raycastTarget = false;

            lines.Add((image, fromPoint, toPoint));
        }

        private void BuildLegend()
        {
            if (legendContainer == null || visuals == null) return;

            foreach (RoomTypeVisualSet.Entry entry in visuals.Entries)
            {
                var row = new GameObject($"Legend_{entry.roomType}", typeof(RectTransform));
                row.transform.SetParent(legendContainer, false);
                var layout = row.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.spacing = 8f;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                var swatchGo = new GameObject("Swatch", typeof(RectTransform));
                swatchGo.transform.SetParent(row.transform, false);
                var swatchLayout = swatchGo.AddComponent<LayoutElement>();
                swatchLayout.preferredWidth = swatchLayout.preferredHeight = 18f;
                var swatch = swatchGo.AddComponent<Image>();
                swatch.color = entry.color;
                if (entry.icon != null) swatch.sprite = entry.icon;
                swatch.raycastTarget = false;

                var textGo = new GameObject("Name", typeof(RectTransform));
                textGo.transform.SetParent(row.transform, false);
                var text = textGo.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 18;
                text.text = visuals.GetName(entry.roomType);
                text.raycastTarget = false;
                var textLayout = textGo.AddComponent<LayoutElement>();
                textLayout.preferredWidth = 80f;
                textLayout.preferredHeight = 22f;
            }
        }

        private void OnNodeClicked(RoomNodeView view)
        {
            RoomSelected?.Invoke(view.Node);
        }
    }
}
