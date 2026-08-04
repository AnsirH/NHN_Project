using System;
using System.Collections;
using OutGame.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// Loading 씬의 진입점 (2026-08-04) — OutGame → Battle 전환 사이에 거치는 중계 씬. 대상 씬 이름은
    /// LoadingHandoff에서 받는다. 실제 로드가 순식간에 끝나도 로딩 화면이 스쳐 지나가듯 사라지지 않게
    /// 최소 표시 시간을 보장한 뒤에만 씬을 활성화한다(사용자 확정, 0.6~0.8초).
    /// </summary>
    public class LoadingScreenController : MonoBehaviour
    {
        [SerializeField] private RectTransform spinner;
        [SerializeField] private Image progressFillImage;
        [SerializeField] private Text statusText;
        [SerializeField] private float minDisplaySeconds = 0.7f;
        [SerializeField] private float spinnerDegreesPerSecond = 220f;

        /// <summary>테스트/툴링에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅 (SceneLoadButton과 동일한 관례).</summary>
        public Func<string, LoadSceneMode, AsyncOperation> LoadSceneAsyncAction = SceneManager.LoadSceneAsync;

        private void Awake()
        {
            if (spinner == null || progressFillImage == null || statusText == null)
                throw new InvalidOperationException("LoadingScreenController 프리팹의 필드가 배선되지 않았습니다.");

            statusText.text = "로딩 중...";
            SetProgress(0f);
        }

        private void Start()
        {
            string targetScene = LoadingHandoff.ConsumeTarget();
            if (string.IsNullOrEmpty(targetScene))
            {
                Debug.Log("[LoadingScreenController] 대기 중인 대상 씬이 없다 — 씬 단독 실행 모드");
                return;
            }

            StartCoroutine(LoadTargetScene(targetScene));
        }

        private void Update()
        {
            spinner.Rotate(0f, 0f, -spinnerDegreesPerSecond * Time.unscaledDeltaTime);
        }

        private IEnumerator LoadTargetScene(string targetScene)
        {
            float startTime = Time.unscaledTime;
            AsyncOperation op = LoadSceneAsyncAction(targetScene, LoadSceneMode.Single);
            op.allowSceneActivation = false;

            while (true)
            {
                SetProgress(Mathf.Clamp01(op.progress / 0.9f));

                bool minTimeElapsed = Time.unscaledTime - startTime >= minDisplaySeconds;
                if (op.progress >= 0.9f && minTimeElapsed)
                {
                    SetProgress(1f);
                    op.allowSceneActivation = true;
                    yield break;
                }

                yield return null;
            }
        }

        private void SetProgress(float value) => progressFillImage.fillAmount = value;
    }
}
