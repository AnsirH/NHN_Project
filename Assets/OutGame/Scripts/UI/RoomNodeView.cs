using System;
using OutGame.Logic.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 방 노드 1개의 뷰 — 프리팹(RoomNodeView.prefab)으로 제작.
    /// 크기·이미지·자식 구조·상태 색은 프리팹 인스펙터에서 수정하고,
    /// 코드는 데이터 주입(Initialize)과 상태 전환(SetState)만 담당한다.
    /// </summary>
    public class RoomNodeView : MonoBehaviour
    {
        public enum NodeState { Locked, Selectable, Current, Visited }

        [Header("프리팹 배선")]
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Outline outline;
        // 2026-08-10: UnityEngine.UI.Text → TMP_Text. 노드 배경이 색 사각형에서 아이콘으로 바뀌면서
        // 라벨도 게임 공용 폰트(DNFForgedBlade-Bold SDF)로 통일했다.
        [SerializeField] private TMP_Text label;

        // 2026-08-10: 예전엔 반투명이 Button의 ColorTint(disabled 틴트 알파 0.5)에서 나왔다. 그런데
        // 그 틴트는 코드가 지정한 색에 곱해지는 데다 interactable=false인 상태 전부에 똑같이 걸려서,
        // (1) 강조돼야 할 현재 노드까지 반투명해지고 (2) 깬 노드와 잠긴 노드가 거의 같은 색이 됐다.
        // 그래서 프리팹의 transition을 None으로 바꾸고 반투명을 여기서 직접 지정한다 —
        // 이제 이 값들이 최종 렌더 색 그대로다(곱해지는 다른 계통 없음).
        [Header("상태 표현 (인스펙터 튜닝)")]
        [SerializeField, Range(0f, 1f)] private float visitedDarken = 1f;   // 1 = 완전한 검정
        [SerializeField, Range(0f, 1f)] private float lockedDarken = 1f;
        [SerializeField, Range(0f, 1f)] private float visitedAlpha = 0.55f; // 깬 방 — 검정 반투명
        [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.35f;  // 아직 못 가는 방 — 더 옅게
        [SerializeField] private Color selectableOutlineColor = Color.white;
        [SerializeField] private Color currentOutlineColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField, Range(0f, 1f)] private float visitedLabelAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float lockedLabelAlpha = 0.35f;

        private Color baseColor;

        public MapNode Node { get; private set; }
        public NodeState State { get; private set; }
        public Button Button => button;

        /// <summary>Selectable 상태의 노드가 클릭됐을 때만 발행된다.</summary>
        public event Action<RoomNodeView> Clicked;

        public void Initialize(MapNode node, RoomTypeVisualSet visuals)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (visuals == null) throw new ArgumentNullException(nameof(visuals));
            if (button == null || background == null || outline == null || label == null)
                throw new InvalidOperationException(
                    "RoomNodeView 프리팹의 button/background/outline/label이 배선되지 않았습니다");

            Node = node;
            baseColor = visuals.GetColor(node.roomType);

            RoomTypeVisualSet.Entry entry = visuals.Get(node.roomType);
            if (entry != null && entry.icon != null)
                background.sprite = entry.icon;

            label.text = visuals.GetName(node.roomType);
            gameObject.name = $"Node_{node.id}";

            button.onClick.RemoveListener(OnButtonClicked);
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
                    outline.effectColor = selectableOutlineColor;
                    button.interactable = true;
                    label.color = Color.white;
                    break;
                case NodeState.Current:
                    background.color = baseColor;
                    outline.enabled = true;
                    outline.effectColor = currentOutlineColor;
                    button.interactable = false;
                    label.color = currentOutlineColor;
                    break;
                case NodeState.Visited:
                    background.color = Fade(Color.Lerp(baseColor, Color.black, visitedDarken), visitedAlpha);
                    outline.enabled = false;
                    button.interactable = false;
                    label.color = new Color(1f, 1f, 1f, visitedLabelAlpha);
                    break;
                default: // Locked
                    background.color = Fade(Color.Lerp(baseColor, Color.black, lockedDarken), lockedAlpha);
                    outline.enabled = false;
                    button.interactable = false;
                    label.color = new Color(1f, 1f, 1f, lockedLabelAlpha);
                    break;
            }
        }

        private static Color Fade(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
