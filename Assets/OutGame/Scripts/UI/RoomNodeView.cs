using System;
using OutGame.Logic.Maps;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 방 노드 1개의 뷰. RoomMapPanel이 런타임에 코드로 생성한다 (별도 프리팹 없음 —
    /// 노드 수가 맵마다 달라 동적 생성이 자연스럽고, 아트 교체 시 이 클래스만 수정).
    /// </summary>
    public class RoomNodeView : MonoBehaviour
    {
        public enum NodeState { Locked, Selectable, Current, Visited }

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Outline outline;
        [SerializeField] private Text label;

        private Color baseColor;

        public MapNode Node { get; private set; }
        public NodeState State { get; private set; }
        public Button Button => button;

        /// <summary>Selectable 상태의 노드가 클릭됐을 때만 발행된다.</summary>
        public event Action<RoomNodeView> Clicked;

        public static RoomNodeView Create(Transform parent, MapNode node, RoomTypeVisualSet visuals, float size)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (visuals == null) throw new ArgumentNullException(nameof(visuals));

            var go = new GameObject($"Node_{node.id}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            var view = go.AddComponent<RoomNodeView>();

            view.background = go.AddComponent<Image>();
            RoomTypeVisualSet.Entry entry = visuals.Get(node.roomType);
            if (entry != null && entry.icon != null)
                view.background.sprite = entry.icon;

            view.outline = go.AddComponent<Outline>();
            view.outline.effectDistance = new Vector2(3f, 3f);
            view.outline.enabled = false;

            view.button = go.AddComponent<Button>();
            view.button.targetGraphic = view.background;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -4f);
            labelRect.sizeDelta = new Vector2(size * 2f, 24f);

            view.label = labelGo.AddComponent<Text>();
            view.label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view.label.fontSize = 16;
            view.label.alignment = TextAnchor.UpperCenter;
            view.label.raycastTarget = false;

            view.Initialize(node, visuals);
            return view;
        }

        private void Initialize(MapNode node, RoomTypeVisualSet visuals)
        {
            Node = node;
            baseColor = visuals.GetColor(node.roomType);
            label.text = visuals.GetName(node.roomType);
            button.onClick.AddListener(OnButtonClicked);
            SetState(NodeState.Locked);
        }

        private void OnButtonClicked()
        {
            if (State != NodeState.Selectable) return;
            Clicked?.Invoke(this);
        }

        public void SetState(NodeState state)
        {
            State = state;
            switch (state)
            {
                case NodeState.Selectable:
                    background.color = baseColor;
                    outline.enabled = true;
                    outline.effectColor = Color.white;
                    button.interactable = true;
                    label.color = Color.white;
                    break;
                case NodeState.Current:
                    background.color = baseColor;
                    outline.enabled = true;
                    outline.effectColor = new Color(1f, 0.85f, 0.2f);
                    button.interactable = false;
                    label.color = new Color(1f, 0.85f, 0.2f);
                    break;
                case NodeState.Visited:
                    background.color = Color.Lerp(baseColor, Color.black, 0.45f);
                    outline.enabled = false;
                    button.interactable = false;
                    label.color = new Color(1f, 1f, 1f, 0.55f);
                    break;
                default: // Locked
                    background.color = Color.Lerp(baseColor, Color.black, 0.65f);
                    outline.enabled = false;
                    button.interactable = false;
                    label.color = new Color(1f, 1f, 1f, 0.35f);
                    break;
            }
        }
    }
}
