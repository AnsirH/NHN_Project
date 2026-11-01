using System;
using System.Collections;
using System.Linq;
using OutGame.Flow;
using OutGame.Logic.Narrative;
using OutGame.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// "의지의 파편" 로딩 오버레이 (Docs/OutGame/로딩 화면 - 의지의 파편 설계.md) — 전투 종료 →
    /// 맵 화면 복귀 전환 시점에만 낮은 확률로 플레이버 텍스트를 보여준다.
    /// 2026-10-19 부활: 파편이 걸릴 때만 오버레이를 띄우고 [계속하기] 클릭을 받는다. 걸리지 않으면
    /// (≈85%) 오버레이를 아예 띄우지 않고 즉시 통과한다 — 폐기 원인이던 "빈 화면 클릭 강제"를 그렇게
    /// 해소했다(§부활 결정).
    ///
    /// 맵 선택→진영, 진영→전투시작 두 지점은 origin의 Loading.unity(LoadingHandoff)가 이미 전담하므로
    /// 이 컴포넌트는 건드리지 않는다 — OutGameFlowController가 소유하는 일반 인스턴스로 충분하고
    /// DontDestroyOnLoad/싱글톤이 필요 없다(OutGame.unity 생애주기 안에서만 쓰이고 씬과 함께
    /// 자연히 파괴됨).
    ///
    /// 2026-08-08: 이름이 범용 "LoadingOverlay"였는데 실제로는 이 용도 하나뿐이라
    /// LoreFragmentOverlayPanel로 개명(Docs/OutGame/화면 명칭 정리.md) — LoreFragmentData/
    /// LoreFragmentSelector/LoreFragmentDefinition과 접두어 통일.
    /// </summary>
    public class LoreFragmentOverlayPanel : MonoBehaviour
    {
        [SerializeField] private GameObject fragmentRoot;
        [SerializeField] private TMP_Text fragmentText; // 2026-08-11: UnityEngine.UI.Text → TMP로 통일
        [SerializeField] private Button continueButton;

        /// <summary>파편 발생 확률. 기본값은 설계 확정값(§부활 결정, 0.15). 테스트에서 0(항상 안 뜸)/
        /// 1(항상 뜸)로 고정해 결정적으로 검증할 수 있도록 공개한다 — 디자인타임에 함부로 건드리지 말 것.</summary>
        [Range(0f, 1f)]
        [Tooltip("파편 발생 확률(0~1). 설계값 0.15 — 테스트 외엔 변경 금지.")]
        public float FragmentTriggerChance = LoreFragmentSelector.DefaultTriggerChance;

        private System.Random rng;
        private LoreFragmentData[] fragmentPool;
        private bool continueClicked;
        private bool initialized;

        private void Awake()
        {
            EnsureInitialized();
            gameObject.SetActive(false);
        }

        /// <summary>풀/rng/버튼 리스너 1회 초기화. Awake에서 부르지만, 비활성 상태로 씬에 놓이면
        /// Awake가 돌지 않아 null이 되는 사고를 막으려 <see cref="Begin"/>에서도 멱등하게 호출한다
        /// (코드 리뷰 반영 — "오버레이는 씬에 활성으로 둬야 한다"는 암묵적 전제를 코드로 강제).</summary>
        private void EnsureInitialized()
        {
            if (initialized) return;

            if (fragmentRoot == null || fragmentText == null || continueButton == null)
                throw new InvalidOperationException("LoreFragmentOverlayPanel의 필드가 배선되지 않았습니다 — 프리팹 구성 확인");

            rng = new System.Random(Environment.TickCount);
            fragmentPool = Resources.LoadAll<LoreFragmentDefinition>(ResourcePaths.LoreFragments)
                .Select(d => d.ToData())
                .ToArray();

            continueButton.onClick.AddListener(() => continueClicked = true);
            initialized = true;
        }

        /// <summary>확률 판정을 먼저 한다. 파편이 걸리지 않으면(≈85%) 오버레이를 띄우지 않고 즉시
        /// onComplete를 호출한다(2026-10-19 부활: 폐기 원인이던 "빈 화면 클릭 강제" 제거 —
        /// Docs/OutGame/로딩 화면 - 의지의 파편 설계.md §부활 결정). 파편이 걸릴 때만 오버레이+텍스트를
        /// 띄우고 [계속하기] 클릭을 받는다.</summary>
        public void Begin(Action onComplete)
        {
            EnsureInitialized(); // 비활성 상태로 Begin이 호출돼도 풀/rng가 null이 아니도록 보장

            if (!LoreFragmentSelector.TrySelect(fragmentPool, rng, FragmentTriggerChance, out LoreFragmentData fragment))
            {
                onComplete?.Invoke();
                return;
            }

            gameObject.SetActive(true);
            StartCoroutine(RunCoroutine(fragment, onComplete));
        }

        private IEnumerator RunCoroutine(LoreFragmentData fragment, Action onComplete)
        {
            fragmentRoot.SetActive(true);
            fragmentText.text = fragment.text; // 파편이 있을 때만 진입하므로 항상 내용이 있다
            fragmentText.gameObject.SetActive(true);

            continueClicked = false;
            yield return new WaitUntil(() => continueClicked); // 파편이 떴을 때만 클릭 대기

            gameObject.SetActive(false);
            onComplete?.Invoke();
        }
    }
}
