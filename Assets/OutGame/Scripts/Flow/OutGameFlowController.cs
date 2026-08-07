using System;
using OutGame.Logic.Audio;
using OutGame.Logic.Runs;
using OutGame.UI;
using UnityEngine;

namespace OutGame.Flow
{
    /// <summary>
    /// OutGame.unity 루트 코디네이터 (§3.1 씬 통합): 맵 선택 → 캐릭터 선택 → 방 그래프 세 패널을
    /// 활성화 토글로 전환시킨다. 이전에는 셋이 각자 씬이라 RunSessionContext(정적 필드)로 넘겼지만,
    /// 이제 같은 씬 안의 형제 GameObject라 확정된 RunState를 이벤트 인자로 직접 전달한다.
    /// RunSessionContext는 씬 교체 전투(§7.4) 복귀 컨텍스트 전달에 여전히 쓰인다 — 그 소비 지점이
    /// 바로 이 클래스의 Start(). 환경 설정은 2026-07-31부터 PausePopup(ESC/뒤로가기로 토글)을 거쳐서만
    /// 열 수 있다 — 이 컨트롤러가 직접 관여하지 않는 자기 완결형 컴포넌트.
    /// </summary>
    public class OutGameFlowController : MonoBehaviour
    {
        [SerializeField] private MapSelectController mapSelectPanel;
        [SerializeField] private CharacterSelectController characterSelectPanel;
        [SerializeField] private InGameFlowController roomGraphController;

        private void Awake()
        {
            if (mapSelectPanel == null || characterSelectPanel == null || roomGraphController == null)
                throw new InvalidOperationException("OutGameFlowController의 필드가 배선되지 않았습니다.");

            // MainMenu를 거치지 않고 OutGame.unity가 곧장 열리는 경로(테스트 등)에 대비한 방어적 적용
            // (MainMenuController.Awake()와 동일 이유).
            AudioListener.volume = SoundSettings.MasterVolume;

            mapSelectPanel.MapConfirmed += OnMapConfirmed;
            characterSelectPanel.CharacterConfirmed += OnCharacterConfirmed;
        }

        private void Start()
        {
            // 이후 검증에서 예외가 나더라도 PendingRun이 stale 상태로 남지 않도록 가장 먼저 소비한다.
            RunState pendingRun = RunSessionContext.ConsumePendingRun();

            if (pendingRun != null)
            {
                // 씬 교체 전투(§7.4)에서 복귀한 경우 — 맵/캐릭터 선택이 이미 끝난 런이라 방 그래프로
                // 곧장 진입한다(2026-07-30: 메인 메뉴 "이어하기" 제거 이후 이 경로의 유일한 발생지).
                //
                // 2026-08-04 사용자 확정: "인게임 → 맵 선택(전투 종료)" 전환에도 로딩 오버레이를 건다.
                // 실제 씬 로드(BattleBridgeConnector.cs, InGame 소유)는 이미 끝난 뒤라 여기서는 순수
                // 파편 게이트 역할만 한다 — roomGraphController를 아직 활성화하지 않은 채로 오버레이가
                // 화면을 가려야 하므로, Begin() 호출 자체를 onComplete 안으로 미룬다.
                LoadingOverlayPanel.GetOrCreate().Begin(() =>
                {
                    ShowOnly(roomGraphController.gameObject);
                    roomGraphController.Begin(pendingRun);
                });
            }
            else
            {
                ShowOnly(mapSelectPanel.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (mapSelectPanel != null) mapSelectPanel.MapConfirmed -= OnMapConfirmed;
            if (characterSelectPanel != null) characterSelectPanel.CharacterConfirmed -= OnCharacterConfirmed;
        }

        private void OnMapConfirmed(RunState run)
        {
            ShowOnly(characterSelectPanel.gameObject);
            characterSelectPanel.Begin(run);
        }

        private void OnCharacterConfirmed(RunState run)
        {
            ShowOnly(roomGraphController.gameObject);
            roomGraphController.Begin(run);
        }

        private void ShowOnly(GameObject target)
        {
            mapSelectPanel.gameObject.SetActive(ReferenceEquals(mapSelectPanel.gameObject, target));
            characterSelectPanel.gameObject.SetActive(ReferenceEquals(characterSelectPanel.gameObject, target));
            roomGraphController.gameObject.SetActive(ReferenceEquals(roomGraphController.gameObject, target));
        }
    }
}
