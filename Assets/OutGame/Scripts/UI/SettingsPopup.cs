using System;
using OutGame.Logic.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 환경 설정 팝업 — 메인 메뉴/아웃게임에서 공용으로 쓴다(1차는 사운드 볼륨 슬라이더 하나).
    /// 실제 값은 OutGame.Logic.Audio.SoundSettings에 저장/조회하고, 여기서는 슬라이더 UI와
    /// AudioListener.volume 적용만 담당한다.
    /// </summary>
    public class SettingsPopup : MonoBehaviour
    {
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (volumeSlider == null || closeButton == null)
                throw new InvalidOperationException("SettingsPopup 프리팹의 필드가 배선되지 않았습니다.");

            closeButton.onClick.AddListener(Hide);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Hide);
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        }

        public void Show()
        {
            volumeSlider.SetValueWithoutNotify(SoundSettings.MasterVolume);
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 다른 팝업 위에 항상 뜨도록
            PanelTransitions.FadeIn(gameObject);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnVolumeChanged(float value)
        {
            SoundSettings.MasterVolume = value;
            AudioListener.volume = value;
        }
    }
}
