using System;
using System.Collections.Generic;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 인벤토리 팝업 (§5.7 — [아이템] 버튼 → 팝업 → 아이템을 부대로 드래그해 부여).
    /// </summary>
    public class InventoryPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform itemContainer;
        [SerializeField] private ItemCardView itemCardPrefab;

        private readonly List<ItemCardView> spawned = new List<ItemCardView>();

        private void Awake()
        {
            if (closeButton == null || itemContainer == null || itemCardPrefab == null)
                throw new InvalidOperationException("InventoryPopup 프리팹의 필드가 배선되지 않았습니다.");

            closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Hide);
        }

        public void Show(IReadOnlyList<string> ownedItemIds, IReadOnlyDictionary<string, ItemDefinition> itemDefs)
        {
            Rebuild(ownedItemIds, itemDefs);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public void Rebuild(IReadOnlyList<string> ownedItemIds, IReadOnlyDictionary<string, ItemDefinition> itemDefs)
        {
            foreach (ItemCardView view in spawned)
                if (view != null) Destroy(view.gameObject);
            spawned.Clear();

            foreach (string itemId in ownedItemIds)
            {
                if (!itemDefs.TryGetValue(itemId, out ItemDefinition def))
                    throw new ArgumentException($"정의되지 않은 아이템 보유 중: {itemId}");

                ItemCardView card = Instantiate(itemCardPrefab, itemContainer);
                card.Initialize(itemId, def.DisplayName, def.Icon);
                spawned.Add(card);
            }
        }
    }
}
