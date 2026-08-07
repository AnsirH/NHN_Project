using System;
using System.Collections;
using System.Linq;
using OutGame.Flow;
using OutGame.Logic.Narrative;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 씬/방 전환 로딩 오버레이 — 맵→진영, 진영→전투시작, 인게임→맵선택(전투 종료) 세 전환 지점에서
    /// 쓰인다(2026-08-04 사용자 확정). 항상 씬을 갈아치우는 것은 아니라 실제 비동기 로딩이 없을 수도
    /// 있으므로, "로딩 자체"는 즉시 끝난 것으로 취급하고 낮은 확률(§2-2 세계관, 기본 15%)로만
    /// "의지의 파편" 텍스트를 잠깐 보여준다 — 파편이 나오면 클릭해야 다음으로 넘어가고, 안 나오면
    /// 곧장 진행된다. 씬 전환(전투 씬 로드 등)을 살아남아야 하므로 DontDestroyOnLoad 싱글톤이다.
    /// </summary>
    public class LoadingOverlayPanel : MonoBehaviour
    {
        private static LoadingOverlayPanel instance;

        [SerializeField] private GameObject spinnerRoot;
        [SerializeField] private GameObject fragmentRoot;
        [SerializeField] private Text fragmentText;
        [SerializeField] private Button continueButton;

        /// <summary>테스트에서 확률을 0으로 고정해 결정적으로 만들 수 있도록 공개 필드로 둔다
        /// (ItemDropConfig를 테스트에서 0/1로 고정하는 기존 패턴과 동일).</summary>
        public float FragmentTriggerChance = LoreFragmentSelector.DefaultTriggerChance;

        private System.Random rng;
        private LoreFragmentData[] fragmentPool;
        private bool continueClicked;

        public static LoadingOverlayPanel GetOrCreate()
        {
            if (instance != null) return instance;

            GameObject prefab = Resources.Load<GameObject>(ResourcePaths.LoadingOverlay);
            if (prefab == null)
                throw new InvalidOperationException(
                    "LoadingOverlay 프리팹을 찾을 수 없습니다 — Resources/OutGame/LoadingOverlay.prefab 확인");

            GameObject go = Instantiate(prefab);
            instance = go.GetComponent<LoadingOverlayPanel>();
            if (instance == null)
                throw new InvalidOperationException("LoadingOverlay 프리팹에 LoadingOverlayPanel 컴포넌트가 없습니다.");

            DontDestroyOnLoad(go);
            return instance;
        }

        /// <summary>테스트 간 정적 싱글톤 오염 방지 — 인스턴스를 파괴하고 참조를 비운다
        /// (BattleBridge.ResetToDefault()와 동일한 목적).</summary>
        public static void ResetForTests()
        {
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        private void Awake()
        {
            if (spinnerRoot == null || fragmentRoot == null || fragmentText == null || continueButton == null)
                throw new InvalidOperationException(
                    "LoadingOverlayPanel의 필드가 배선되지 않았습니다 — 프리팹 구성 확인");

            rng = new System.Random(Environment.TickCount);
            fragmentPool = Resources.LoadAll<LoreFragmentDefinition>(ResourcePaths.LoreFragments)
                .Select(f => f.ToData()).ToArray();

            continueButton.onClick.AddListener(() => continueClicked = true);
            gameObject.SetActive(false);
        }

        /// <summary>전환 게이트를 시작한다 — 낮은 확률로 파편이 뽑히면 클릭 대기 후, 아니면 곧장
        /// onComplete를 부른다. 씬 전환 트리거(LoadSceneAction 등)는 onComplete 안에서 호출해야
        /// 오버레이가 화면을 가린 상태에서 실제 전환이 일어난다.</summary>
        public void Begin(Action onComplete)
        {
            LoreFragmentSelector.TrySelect(fragmentPool, rng, FragmentTriggerChance, out LoreFragmentData fragment);

            gameObject.SetActive(true);
            StartCoroutine(RunCoroutine(fragment, onComplete));
        }

        private IEnumerator RunCoroutine(LoreFragmentData fragment, Action onComplete)
        {
            spinnerRoot.SetActive(fragment == null);
            fragmentRoot.SetActive(fragment != null);

            if (fragment != null)
            {
                continueClicked = false;
                fragmentText.text = fragment.text;
                yield return new WaitUntil(() => continueClicked);
            }

            gameObject.SetActive(false);
            onComplete?.Invoke();
        }
    }
}
