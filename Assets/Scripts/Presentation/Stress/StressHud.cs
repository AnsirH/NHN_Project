using TMPro;
using UnityEngine;

namespace NHN.Presentation.Stress
{
    /// <summary>FPS와 유닛 수 표시. 텍스트 갱신은 주기적으로만 수행해 할당을 억제한다.</summary>
    public sealed class StressHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text fpsText;
        [SerializeField] private TMP_Text unitCountText;
        [SerializeField] private float displayInterval = 0.5f;
        [SerializeField] private float smoothing = 0.1f;

        private StressTestBootstrap _bootstrap;
        private float _smoothedDeltaTime;
        private float _nextDisplayTime;

        public void Initialize(StressTestBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            _smoothedDeltaTime = Time.unscaledDeltaTime;
        }

        public void DisplayUnitCount(int unitCount)
        {
            unitCountText.SetText("Units: {0}", unitCount);
        }

        /// <summary>씬의 유닛 수 버튼 OnClick(persistent listener)에서 호출된다.</summary>
        public void OnUnitCountButton(int unitCount)
        {
            _bootstrap.SetUnitCount(unitCount);
        }

        private void Update()
        {
            _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime, Time.unscaledDeltaTime, smoothing);

            if (Time.unscaledTime >= _nextDisplayTime)
            {
                _nextDisplayTime = Time.unscaledTime + displayInterval;
                fpsText.SetText("{0:0} FPS  ({1:0.0} ms)", 1f / _smoothedDeltaTime, _smoothedDeltaTime * 1000f);
            }
        }
    }
}
