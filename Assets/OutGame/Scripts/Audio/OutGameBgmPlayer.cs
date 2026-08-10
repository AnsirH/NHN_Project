using UnityEngine;

namespace OutGame.Audio
{
    /// <summary>
    /// 아웃게임 배경음 (2026-08-10) — OutGame.unity에 하나만 두고 씬이 살아있는 동안 반복 재생한다.
    ///
    /// <b>일부러 DontDestroyOnLoad를 쓰지 않는다.</b> 전투는 씬 자체를 교체하는데(§7.4), 전투 씬
    /// (Battle.unity)은 자기 배경음(ms_Ingame_*)을 따로 물고 있다. 이 재생기가 씬을 넘어 살아남으면
    /// 전투 중에 아웃게임 음악이 겹쳐 흐른다 — 그래서 씬과 함께 사라지게 두고, 대신 재생 위치만
    /// 정적으로 남겨 복귀 시 이어 재생한다(<see cref="resumeTime"/>).
    ///
    /// 볼륨은 AudioListener.volume(환경설정 마스터 볼륨, SoundSettings)에 이 컴포넌트의
    /// <see cref="volume"/>이 곱해진 값이다 — 마스터를 건드리지 않고 배경음만 낮춰 둘 수 있다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class OutGameBgmPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;

        // 사용자 요청(2026-08-10): "너무 시끄럽지 않게". 마스터 볼륨에 곱해지는 값이라 0.35면
        // 마스터가 최대여도 배경음은 35% 세기로 깔린다.
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;

        // 씬이 열리자마자 최대 음량으로 시작하면 갑작스러우므로 짧게 페이드 인한다.
        [SerializeField, Range(0f, 5f)] private float fadeInSeconds = 1.5f;

        /// <summary>전투 복귀 때 곡을 처음부터 다시 틀지 않도록 남겨두는 재생 위치(초).
        /// 정적이라 씬 교체를 넘어 유지되고, 게임을 껐다 켜면 자연히 0으로 돌아간다.</summary>
        private static float resumeTime;

        private AudioSource source;
        private float fadeElapsed;

        // 씬 언로드 때 Unity가 OnDestroy보다 먼저 AudioSource를 정지시켜, 그 시점의 source.time은
        // 0으로 읽힌다(2026-08-10 실측). 그래서 매 프레임 위치를 들고 있다가 그 값을 넘긴다.
        private float lastKnownTime;

        private void Awake()
        {
            source = GetComponent<AudioSource>();

            // 배경음이 없다고 게임이 멈출 이유는 없다 — 이 프로젝트의 다른 배선 검사와 달리 예외를
            // 던지지 않고 경고만 남기고 스스로 꺼진다(연출 요소라 의도적으로 다르게 처리).
            if (clip == null)
            {
                Debug.LogWarning("[OutGameBgmPlayer] 재생할 클립이 배선되지 않아 배경음을 끕니다.");
                enabled = false;
                return;
            }

            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D — 카메라와의 거리에 따라 음량이 변하면 안 된다
            source.volume = 0f;       // 아래 Update에서 fadeInSeconds에 걸쳐 volume까지 올린다

            // 재생 위치는 반드시 Play() "뒤에" 지정한다 — loadType이 Streaming이면 Play() 전에 준
            // time은 무시되고 0부터 시작한다(2026-08-10 실측으로 확인).
            source.Play();
            if (resumeTime > 0f && clip.length > 0f)
                source.time = resumeTime % clip.length;
        }

        private void Update()
        {
            if (source.isPlaying)
                lastKnownTime = source.time;

            if (fadeElapsed >= fadeInSeconds)
                return;

            fadeElapsed += Time.unscaledDeltaTime; // 일시정지(timeScale=0) 중에도 자연스럽게 올라오도록
            source.volume = fadeInSeconds <= 0f
                ? volume
                : Mathf.Lerp(0f, volume, Mathf.Clamp01(fadeElapsed / fadeInSeconds));
        }

        private void OnDestroy()
        {
            // 씬 교체(전투 진입)로 파괴될 때의 재생 위치를 남긴다 — 복귀한 새 인스턴스가 이어 받는다.
            resumeTime = lastKnownTime;
        }

        /// <summary>인스펙터에서 volume을 조정하면 재생 중에도 즉시 반영되게 한다(값 맞추기 편의).</summary>
        private void OnValidate()
        {
            if (Application.isPlaying && source != null && fadeElapsed >= fadeInSeconds)
                source.volume = volume;
        }
    }
}
