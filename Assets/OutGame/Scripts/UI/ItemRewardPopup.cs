using System;
using System.Collections.Generic;
using OutGame.ScriptableObjects;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 전투 승리 시 획득한 아이템 알림 팝업 (2026-07-26 사용자 요청) — 그 전까지는 드롭이
    /// `run.ownedItemIds`에 조용히 추가되기만 하고 화면 표시가 전혀 없었다. EventPanel/RestPanel/
    /// AugmentPanel과 달리 확인 전까지 뒤 화면을 막는 모달이라 dim 배경을 둔다(다른 두 배치 화면
    /// 팝업, InventoryPopup/ArmyInfoPopup은 드래그 앤 드롭 작업 중 뒤가 보여야 해서 dim이 없는 것과
    /// 대비됨).
    /// </summary>
    public class ItemRewardPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform itemContainer;
        [SerializeField] private ItemCardView itemCardPrefab;
        [SerializeField] private Button closeButton;

        private readonly List<ItemCardView> spawned = new List<ItemCardView>();

        /// <summary>확인 버튼으로 팝업을 닫았을 때 발행 — 호출부가 이후 진행(방 그래프 복귀 등)을 이어간다.</summary>
        public event Action Closed;

        private void Awake()
        {
            if (itemContainer == null || itemCardPrefab == null || closeButton == null)
                throw new InvalidOperationException("ItemRewardPopup 프리팹의 필드가 배선되지 않았습니다.");

            closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        /// <summary>드롭된 아이템 목록을 카드로 표시하고 팝업을 연다. 같은 아이템이 중복 드롭돼도(예:
        /// 같은 병과 적 2마리 모두 성공) 카드도 그만큼 중복 표시한다 — 보유 목록도 동일하게 중복 허용.</summary>
        public void Open(IReadOnlyList<string> droppedItemIds, IReadOnlyDictionary<string, ItemDefinition> itemDefs)
        {
            if (droppedItemIds == null) throw new ArgumentNullException(nameof(droppedItemIds));
            if (itemDefs == null) throw new ArgumentNullException(nameof(itemDefs));
            if (droppedItemIds.Count == 0)
                throw new ArgumentException("드롭된 아이템이 없습니다 — 호출부에서 Count > 0일 때만 열어야 합니다.", nameof(droppedItemIds));

            foreach (ItemCardView view in spawned)
                if (view != null) Destroy(view.gameObject);
            spawned.Clear();

            foreach (string itemId in droppedItemIds)
            {
                if (!itemDefs.TryGetValue(itemId, out ItemDefinition def))
                    throw new ArgumentException($"정의되지 않은 아이템 드롭: {itemId}");

                ItemCardView card = Instantiate(itemCardPrefab, itemContainer);
                card.Initialize(itemId, def.DisplayName, def.Icon);
                spawned.Add(card);
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 다른 팝업들과 동일 관례 — 항상 최상단에 떠야 함
        }

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }
    }
}
