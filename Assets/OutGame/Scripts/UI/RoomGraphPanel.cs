using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Flow;
using OutGame.Logic.Maps;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 방 그래프 UI (상세 기획 §5.3). 씬 독립 프리팹 — 어떤 Canvas 아래든 붙여서 사용.
    /// 사용법: Open(MapState) → 노드 선택 시 RoomSelected 발행 → 방 처리 후 Refresh() 호출.
    /// 방문 확정(MapProgress.Visit)은 이 패널의 책임이 아니다 (플로우 쪽 결정).
    ///
    /// 비주얼(노드/연결선/범례의 크기·이미지·색)은 주입된 프리팹의 인스펙터에서 수정한다.
    /// 코드는 데이터 기반 배치(개수·위치·상태)만 담당.
    ///
    /// 2026-08-08: RoomMapPanel → RoomGraphPanel로 개명(Docs/OutGame/화면 명칭 정리.md) — 상세
    /// 기획서·씬 오브젝트명(RoomGraphRoot)이 전부 "방 그래프"인데 클래스만 "Map"이라 어긋나 있었다.
    /// </summary>
    public class RoomGraphPanel : MonoBehaviour
    {
        [Header("구조 참조")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform lineLayer;
        [SerializeField] private RectTransform nodeLayer;
        [SerializeField] private RectTransform legendContainer;
        [SerializeField] private CurrencyDisplay currencyDisplay; // 2026-07-26: 범례가 있던 우측 상단 자리로 이동
        [SerializeField] private Button formationButton; // 2026-07-26: 좌측 하단 "진영" 버튼
        [SerializeField] private Image playerImage; // 2026-08-10: 상단 바의 선택된 캐릭터 초상화
        [SerializeField] private Button settingsButton; // 2026-08-10: 상단 바 "환경설정"

        [Header("요소 프리팹 (비주얼은 각 프리팹에서 수정)")]
        [SerializeField] private RoomNodeView nodePrefab;
        [SerializeField] private Image linePrefab;
        [SerializeField] private LegendEntry legendEntryPrefab;

        [Header("표시 설정")]
        [SerializeField] private RoomTypeVisualSet visuals;
        [SerializeField] private float unitScale = 110f;   // MapNode.posX/posY(논리 단위) → 픽셀
        [SerializeField] private float contentPadding = 90f;
        [SerializeField] private Color lineDimColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color lineTraveledColor = new Color(1f, 0.85f, 0.2f, 0.85f);

        [Header("연결선 스타일 (손그림 점선 — OutGame/UI/DashedWobblyLine 셰이더)")]
        [SerializeField] private float lineThickness = 5f;
        [SerializeField] private float lineDashLength = 14f;
        [SerializeField] private float lineGapLength = 9f;
        [SerializeField] private float lineWobbleAmplitude = 10f; // 주 굴곡 최대 진폭
        [SerializeField] private float lineJitterAmplitude = 3f;  // 손떨림 잔진폭
        [SerializeField] private float lineJitterFrequency = 5f;  // 손떨림 반복 빈도
        [SerializeField] private float lineAntiAlias = 1.5f;
        [SerializeField, Range(0f, 1f)] private float lineBlobSizeJitter = 0.35f; // 점선 조각 크기 랜덤 편차
        [SerializeField, Range(0f, 1f)] private float lineBlobBumpAmount = 0.3f;  // 점선 조각 울퉁불퉁함

        private Material lineMaterialBase;
        private readonly List<RoomNodeView> nodeViews = new List<RoomNodeView>();
        private readonly List<(Image image, GridPoint from, GridPoint to)> lines
            = new List<(Image, GridPoint, GridPoint)>();

        public MapState Map { get; private set; }
        public IReadOnlyList<RoomNodeView> NodeViews => nodeViews;

        /// <summary>선택 가능한 노드가 클릭됐을 때 발행. 방문 확정은 구독자 책임.</summary>
        public event Action<MapNode> RoomSelected;

        /// <summary>"진영" 버튼 클릭 시 발행 — 진영 팝업을 여는 것은 구독자(InGameFlowController) 책임.</summary>
        public event Action FormationRequested;

        /// <summary>"환경설정" 버튼 클릭 시 발행 (2026-08-10) — 무엇을 열지는 구독자 책임이다.
        /// 현재 구독자는 일시정지 팝업을 연다(그 안에 환경설정 + 메인메뉴 복귀가 모두 있다).
        /// 이 패널이 직접 참조하지 않는 이유: PausePopup은 씬 오브젝트라 프리팹인 이 패널이 참조를
        /// 가질 수 없다. FormationRequested와 같은 구조.</summary>
        public event Action SettingsRequested;


        private void Awake()
        {
            formationButton.onClick.AddListener(OnFormationButtonClicked);
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }

        private void OnDestroy()
        {
            formationButton.onClick.RemoveListener(OnFormationButtonClicked);
            settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
        }

        private void OnFormationButtonClicked() => FormationRequested?.Invoke();

        private void OnSettingsButtonClicked() => SettingsRequested?.Invoke();

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
                    "RoomGraphPanel의 content/lineLayer/nodeLayer가 배선되지 않았습니다 — 프리팹 구성(SceneSetupM2) 확인");
            if (nodePrefab == null || linePrefab == null || legendEntryPrefab == null)
                throw new InvalidOperationException(
                    "RoomGraphPanel의 nodePrefab/linePrefab/legendEntryPrefab이 배선되지 않았습니다");
            if (currencyDisplay == null)
                throw new InvalidOperationException("RoomGraphPanel의 currencyDisplay가 배선되지 않았습니다");
            if (formationButton == null)
                throw new InvalidOperationException("RoomGraphPanel의 formationButton이 배선되지 않았습니다");
            if (playerImage == null)
                throw new InvalidOperationException("RoomGraphPanel의 playerImage가 배선되지 않았습니다");
            if (settingsButton == null)
                throw new InvalidOperationException("RoomGraphPanel의 settingsButton이 배선되지 않았습니다");
            if (visuals == null)
                throw new InvalidOperationException(
                    "RoomGraphPanel.visuals(RoomTypeVisualSet)가 할당되지 않았습니다");

            if (lineMaterialBase == null)
                lineMaterialBase = Resources.Load<Material>(ResourcePaths.MapLineMaterial);
            if (lineMaterialBase == null)
                throw new InvalidOperationException(
                    $"RoomGraphPanel의 점선 재질({ResourcePaths.MapLineMaterial})을 찾을 수 없습니다");
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>Close()로 감춘 뒤 방 처리가 끝나 방 그래프로 복귀할 때 다시 보여준다 — Open()과
        /// 달리 재구성(Rebuild)하지 않는다(상태 갱신은 별도로 Refresh() 호출 책임).</summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary>재화 표시를 갱신한다 — 이벤트/전투 보상으로 골드가 바뀐 뒤 맵으로 돌아올 때 호출.</summary>
        public void SetGold(int amount) => currencyDisplay.SetAmount(amount);

        /// <summary>상단 바에 선택된 캐릭터의 아이콘을 표시한다 — 런 시작 시 InGameFlowController가 호출.
        /// 캐릭터 id → 정의 조회는 이미 캐릭터 풀을 들고 있는 호출자 책임이다(이 패널은 표시만 담당).
        /// 전신 초상화가 아니라 아이콘을 쓴다 — 이 슬롯은 100px이라 대형 이미지를 넣으면 얼굴이 뭉개진다.
        /// 아이콘이 없는 캐릭터면 이미지를 숨긴다 — 스프라이트가 빈 Image는 흰 사각형으로 그려진다.</summary>
        public void SetPlayerIcon(Sprite icon)
        {
            playerImage.sprite = icon;
            playerImage.enabled = icon != null;
        }

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
                image.color = IsTraveledEdge(from, to) ? lineTraveledColor : lineDimColor;
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
            float nodeSize = ((RectTransform)nodePrefab.transform).sizeDelta.x;

            foreach (MapNode node in Map.nodes)
            {
                foreach (GridPoint dest in node.outgoing)
                {
                    if (!positions.TryGetValue(dest, out Vector2 destPos))
                        throw new ArgumentException(
                            $"손상된 맵 데이터 — {node.point}의 연결 대상 {dest} 노드가 없습니다");
                    CreateLine(positions[node.point], destPos, node.point, dest, nodeSize);
                }
            }

            foreach (MapNode node in Map.nodes)
            {
                RoomNodeView view = Instantiate(nodePrefab, nodeLayer);
                view.Initialize(node, visuals);
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
            {
                if (image == null) continue;
                if (image.material != null) Destroy(image.material); // CreateLine에서 매번 새 인스턴스를 만들므로 직접 정리
                Destroy(image.gameObject);
            }
            lines.Clear();

            if (legendContainer != null)
                for (int i = legendContainer.childCount - 1; i >= 0; i--)
                    Destroy(legendContainer.GetChild(i).gameObject);
        }

        private void CreateLine(Vector2 from, Vector2 to, GridPoint fromPoint, GridPoint toPoint, float nodeSize)
        {
            Image image = Instantiate(linePrefab, lineLayer);
            image.gameObject.name = $"Line_{fromPoint}_{toPoint}";
            image.color = lineDimColor;

            Vector2 delta = to - from;
            float length = Mathf.Max(0f, delta.magnitude - nodeSize); // 노드에 겹치지 않게 축소

            var rect = (RectTransform)image.transform;
            float wobbleBudget = (lineWobbleAmplitude + lineJitterAmplitude) * 2f + lineThickness;
            rect.sizeDelta = new Vector2(length, wobbleBudget); // 곡선이 불룩해질 여유 높이
            rect.anchoredPosition = from + delta.normalized * (nodeSize / 2f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            ApplyLineMaterial(image, fromPoint, toPoint, length, wobbleBudget);

            lines.Add((image, fromPoint, toPoint));
        }

        /// <summary>
        /// 선마다 굴곡/손떨림 위상을 From/To 좌표 해시로 고정해 매번 다른 모양을 주되, 리빌드해도
        /// 항상 같은 모양이 나오게 한다(손그림 지도 느낌 — 프레임마다/재구성마다 흔들리면 안 됨).
        /// </summary>
        private void ApplyLineMaterial(Image image, GridPoint fromPoint, GridPoint toPoint, float length, float height)
        {
            int seed = fromPoint.x * 73856093 ^ fromPoint.y * 19349663
                ^ toPoint.x * 83492791 ^ toPoint.y * -1640531527;
            float amplitudeSign = HashToUnit(seed) < 0.5f ? -1f : 1f;
            float amplitude1 = amplitudeSign *
                Mathf.Lerp(lineWobbleAmplitude * 0.5f, lineWobbleAmplitude, HashToUnit(seed + 1));
            float phase2 = HashToUnit(seed + 2) * Mathf.PI * 2f;

            Material material = new Material(lineMaterialBase);
            material.SetFloat("_Length", length);
            material.SetFloat("_Height", height);
            material.SetFloat("_Thickness", lineThickness);
            material.SetFloat("_DashLength", lineDashLength);
            material.SetFloat("_GapLength", lineGapLength);
            material.SetFloat("_Amplitude1", amplitude1);
            material.SetFloat("_Amplitude2", lineJitterAmplitude);
            material.SetFloat("_Freq2", lineJitterFrequency);
            material.SetFloat("_Phase2", phase2);
            material.SetFloat("_AA", lineAntiAlias);
            material.SetFloat("_BlobSizeJitter", lineBlobSizeJitter);
            material.SetFloat("_BlobBumpAmount", lineBlobBumpAmount);
            image.material = material;
        }

        private static float HashToUnit(int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= h >> 16; h *= 0x7feb352dU;
                h ^= h >> 15; h *= 0x846ca68bU;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0xFFFFFF;
            }
        }

        private void BuildLegend()
        {
            if (legendContainer == null) return;

            foreach (RoomTypeVisualSet.Entry entry in visuals.Entries)
            {
                LegendEntry row = Instantiate(legendEntryPrefab, legendContainer);
                row.gameObject.name = $"Legend_{entry.roomType}";
                row.Setup(entry, visuals.GetName(entry.roomType));
            }
        }

        private void OnNodeClicked(RoomNodeView view)
        {
            RoomSelected?.Invoke(view.Node);
        }
    }
}
