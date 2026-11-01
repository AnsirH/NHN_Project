using System.Collections;
using NUnit.Framework;
using OutGame.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// "의지의 파편" 로딩 오버레이(LoreFragmentOverlayPanel) 단독 검증 — 확률 판정 자체는
    /// LoreFragmentSelectorTests(EditMode)가 담당하고, 여기서는 프리팹 배선 + 클릭 대기 흐름만
    /// 확인한다. 결정성 확보를 위해 FragmentTriggerChance를 0/1로 고정한다(설계 문서 §7).
    /// </summary>
    public class LoreFragmentOverlayPanelPlayTests
    {
        private GameObject instance;
        private LoreFragmentOverlayPanel panel;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = Resources.Load<GameObject>("OutGame/Overlays/LoreFragmentOverlay");
            Assert.IsNotNull(prefab, "LoreFragmentOverlay 프리팹 없음 — BuildLoadingOverlayPrefab.Run() 실행 필요");
            instance = Object.Instantiate(prefab);
            panel = instance.GetComponent<LoreFragmentOverlayPanel>();
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null) Object.Destroy(instance);
        }

        private Button ContinueButton() => PrefabBinding.Get<Button>(panel, "continueButton");
        private TMP_Text FragmentText() => PrefabBinding.Get<TMP_Text>(panel, "fragmentText");

        // 2026-10-19 부활: 파편이 안 뜨면(≈85%) 오버레이를 띄우지 않고 즉시 통과한다
        // (폐기 원인이던 "빈 화면 클릭 강제" 제거 — Docs/OutGame/로딩 화면 - 의지의 파편 설계.md §부활 결정).
        // 그래서 "확률0에도 오버레이가 뜨고 클릭을 기다린다"는 옛 계약 테스트는 삭제하고 아래로 교체했다.

        [UnityTest]
        public IEnumerator Begin_NoFragment_AutoPassesWithoutShowingOverlay()
        {
            panel.FragmentTriggerChance = 0f; // 파편 절대 안 뜸
            bool completed = false;

            panel.Begin(() => completed = true);
            yield return null;

            Assert.IsTrue(completed, "파편이 안 뜨면 클릭 없이 즉시 onComplete가 호출돼야 함(폐기 원인 해소)");
            Assert.IsFalse(instance.activeSelf, "파편이 안 뜨면 오버레이는 아예 표시되지 않아야 함");
        }

        [UnityTest]
        public IEnumerator Begin_FragmentTriggered_ShowsOverlayWithTextAndWaitsForClick()
        {
            panel.FragmentTriggerChance = 1f; // 파편 항상 뜸
            bool completed = false;

            panel.Begin(() => completed = true);
            yield return null;

            Assert.IsTrue(instance.activeSelf, "파편이 뜨면 오버레이가 보여야 함");
            Assert.IsTrue(FragmentText().gameObject.activeSelf, "파편이 뜨면 텍스트가 보여야 함");
            Assert.IsNotEmpty(FragmentText().text);
            Assert.IsFalse(completed, "클릭 전엔 onComplete가 호출되면 안 됨");
        }

        [UnityTest]
        public IEnumerator ContinueClick_WhenFragmentShown_InvokesOnCompleteAndHidesOverlay()
        {
            panel.FragmentTriggerChance = 1f;
            bool completed = false;
            panel.Begin(() => completed = true);
            yield return null;

            ContinueButton().onClick.Invoke();
            yield return null;

            Assert.IsTrue(completed, "클릭하면 onComplete가 호출돼야 함");
            Assert.IsFalse(instance.activeSelf, "클릭 후 오버레이는 스스로 비활성화돼야 함");
        }
    }
}
