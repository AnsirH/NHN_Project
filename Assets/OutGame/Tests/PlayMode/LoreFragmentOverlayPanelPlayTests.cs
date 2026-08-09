using System.Collections;
using NUnit.Framework;
using OutGame.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        private Button ContinueButton() => instance.transform.Find("FragmentRoot/ContinueButton").GetComponent<Button>();
        private Text FragmentText() => instance.transform.Find("FragmentRoot/FragmentText").GetComponent<Text>();

        [UnityTest]
        public IEnumerator Begin_ActivatesOverlay()
        {
            panel.FragmentTriggerChance = 0f;
            bool completed = false;

            panel.Begin(() => completed = true);
            yield return null;

            Assert.IsTrue(instance.activeSelf, "Begin() 호출 시 오버레이가 즉시 보여야 함");
            Assert.IsFalse(completed, "클릭 전엔 onComplete가 호출되면 안 됨");
        }

        [UnityTest]
        public IEnumerator Begin_ChanceZero_HidesFragmentTextButStillWaitsForClick()
        {
            panel.FragmentTriggerChance = 0f;
            panel.Begin(() => { });
            yield return null;

            Assert.IsFalse(FragmentText().gameObject.activeSelf, "파편이 안 뜨면 텍스트는 숨겨야 함");
            Assert.IsTrue(instance.activeSelf, "파편 유무와 무관하게 클릭 전까진 오버레이가 유지돼야 함");
        }

        [UnityTest]
        public IEnumerator Begin_ChanceOne_ShowsFragmentTextWithNonEmptyText()
        {
            panel.FragmentTriggerChance = 1f;
            panel.Begin(() => { });
            yield return null;

            Assert.IsTrue(FragmentText().gameObject.activeSelf, "확률에 걸리면 파편 텍스트가 보여야 함");
            Assert.IsNotEmpty(FragmentText().text);
        }

        [UnityTest]
        public IEnumerator ContinueClick_InvokesOnCompleteAndHidesOverlay_RegardlessOfFragment()
        {
            panel.FragmentTriggerChance = 1f; // 파편이 뜨는 경우에도 클릭하면 넘어가야 함을 확인
            bool completed = false;
            panel.Begin(() => completed = true);
            yield return null;

            ContinueButton().onClick.Invoke();
            yield return null;

            Assert.IsTrue(completed, "클릭하면 파편 유무와 무관하게 onComplete가 호출돼야 함");
            Assert.IsFalse(instance.activeSelf, "클릭 후 오버레이는 스스로 비활성화돼야 함");
        }
    }
}
