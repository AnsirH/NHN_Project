using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.ScriptableObjects;
using OutGame.UI;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// 전투 승리 아이템 획득 알림 팝업 검증 (2026-07-26 사용자 요청).
    /// </summary>
    public class ItemRewardPopupPlayTests
    {
        private GameObject canvasGo;
        private ItemRewardPopup popup;
        private Dictionary<string, ItemDefinition> itemDefs;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas));
            GameObject prefab = Resources.Load<GameObject>("OutGame/ItemRewardPopup");
            Assert.IsNotNull(prefab, "ItemRewardPopup 프리팹이 없음 — SceneSetupM4UI.Run() 실행 필요");
            popup = Object.Instantiate(prefab, canvasGo.transform).GetComponent<ItemRewardPopup>();

            var bowDef = Resources.Load<ItemDefinition>("OutGame/Data/ItemDefinition_Bow");
            var shieldDef = Resources.Load<ItemDefinition>("OutGame/Data/ItemDefinition_Shield");
            Assert.IsNotNull(bowDef);
            Assert.IsNotNull(shieldDef);
            itemDefs = new Dictionary<string, ItemDefinition> { ["item_bow"] = bowDef, ["item_shield"] = shieldDef };
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
        }

        [UnityTest]
        public IEnumerator Open_SpawnsOneCardPerDroppedItem()
        {
            popup.Open(new List<string> { "item_bow", "item_shield" }, itemDefs);
            yield return null;

            var cards = popup.GetComponentsInChildren<ItemCardView>();
            Assert.AreEqual(2, cards.Length);
            Assert.IsTrue(popup.gameObject.activeSelf, "Open() 호출 후 팝업이 보여야 함");
        }

        [UnityTest]
        public IEnumerator Open_DuplicateItemId_SpawnsDuplicateCards()
        {
            // 같은 병과 적이 여럿이면 같은 아이템이 중복 드롭될 수 있다 — 카드도 중복 표시돼야 한다
            // (보유 목록도 동일하게 중복 허용, BattleRewardApplier 기존 관례와 대칭).
            popup.Open(new List<string> { "item_bow", "item_bow" }, itemDefs);
            yield return null;

            var cards = popup.GetComponentsInChildren<ItemCardView>();
            Assert.AreEqual(2, cards.Length);
        }

        [UnityTest]
        public IEnumerator Open_CalledTwice_ClearsPreviousCardsBeforeRespawning()
        {
            popup.Open(new List<string> { "item_bow", "item_shield" }, itemDefs);
            yield return null;

            popup.Open(new List<string> { "item_bow" }, itemDefs);
            yield return null;

            var cards = popup.GetComponentsInChildren<ItemCardView>();
            Assert.AreEqual(1, cards.Length, "재호출 시 이전 카드가 남아있으면 안 됨");
        }

        [UnityTest]
        public IEnumerator CloseButton_HidesPopupAndFiresClosedEvent()
        {
            popup.Open(new List<string> { "item_bow" }, itemDefs);
            yield return null;

            bool closedFired = false;
            popup.Closed += () => closedFired = true;

            Transform closeButtonTransform = popup.transform.Find("Window/CloseButton");
            closeButtonTransform.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            Assert.IsFalse(popup.gameObject.activeSelf, "확인 버튼 클릭 후 팝업이 닫혀야 함");
            Assert.IsTrue(closedFired, "확인 버튼 클릭 후 Closed 이벤트가 발행돼야 함");
        }

        [UnityTest]
        public IEnumerator ItemRewardPopup_HasDimBackground()
        {
            // 사용자 명시 요구: 이 팝업은 dim 처리 필요 — 다른 두 배치 화면 팝업(인벤토리/군대 정보)과 반대.
            Assert.IsNotNull(popup.GetComponent<UnityEngine.UI.Image>(), "ItemRewardPopup 루트에는 dim Image가 있어야 함");
            yield break;
        }

        [UnityTest]
        public IEnumerator Open_UnknownItemId_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                popup.Open(new List<string> { "item_unknown" }, itemDefs));
            yield break;
        }

        [UnityTest]
        public IEnumerator Open_NullArguments_Throw()
        {
            Assert.Throws<System.ArgumentNullException>(() => popup.Open(null, itemDefs));
            Assert.Throws<System.ArgumentNullException>(() => popup.Open(new List<string>(), null));
            yield break;
        }
    }
}
