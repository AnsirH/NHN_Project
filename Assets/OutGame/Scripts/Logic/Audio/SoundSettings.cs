using UnityEngine;

namespace OutGame.Logic.Audio
{
    /// <summary>
    /// 마스터 볼륨 설정 — PlayerPrefs로 영구 저장한다. 값 저장/조회만 담당하고
    /// AudioListener.volume 적용은 호출자(UI/플로우 컨트롤러) 책임이다. OutGame.Logic 소속이라
    /// NHN.Integration이 이미 참조 중(SceneNames.cs와 동일 이유) — 인게임 쪽 환경설정 UI를 만들 때도
    /// 이 클래스 하나로 아웃게임과 같은 값을 읽고 쓰면 된다.
    /// </summary>
    public static class SoundSettings
    {
        public const string VolumeKey = "OutGame.MasterVolume";
        public const float DefaultVolume = 1f;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            set => PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
        }
    }
}
