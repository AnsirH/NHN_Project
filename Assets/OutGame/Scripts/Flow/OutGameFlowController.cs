using System;
using OutGame.Logic.Audio;
using OutGame.Logic.Runs;
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
    ///
    /// pendingRun이 있는 경우 = 전투 종료 후 이 씬이 다시 로드된 경우(§7.4) — 방 그래프로 곧장
    /// 진입한다.
    ///
    /// 2026-08-10: 파편이 뜨지 않는 경우에도 검은 화면에서 매번 [계속하기]를 눌러야 해서 전투 복귀가
    /// 번거롭다는 이유로 LoreFragmentOverlayPanel("의지의 파편")을 흐름에서 뺐었다.
    /// 2026-10-19 부활: 그 폐기 원인을 "파편이 안 걸리면 오버레이를 아예 띄우지 않고 즉시 통과"로
    /// 개선해 되살렸다(§부활 결정). 아래 pendingRun 분기가 방 그래프 진입을 loreOverlay.Begin(...)
    /// 콜백으로 감싸며, loreOverlay는 선택 필드(미배선이면 직행)다.
    ///
    /// 맵 선택→진영, 진영→전투시작 두 지점은 origin의 Loading.unity(LoadingHandoff)가 전담하므로
    /// 여기서 건드리지 않는다.
    /// </summary>
    public class OutGameFlowController : MonoBehaviour
    {
        [SerializeField] private MapSelectPanel mapSelectPanel;
        [SerializeField] private CharacterSelectPanel characterSelectPanel;
        [SerializeField] private InGameFlowController roomGraphController;

        // 선택(graceful skip): 미배선(null)이면 오버레이를 건너뛰고 방 그래프로 직행한다. 장식적
        // 연출이 전투 복귀라는 핵심 경로를 막지 않게 하려는 의도적 예외(프로젝트의 fail-fast null
        // 체크 관례와 다름 — Docs/OutGame/로딩 화면 - 의지의 파편 설계.md §부활 결정 B-2).
        [SerializeField] private OutGame.UI.LoreFragmentOverlayPanel loreOverlay;

        private void Awake()
        {
            if (mapSelectPanel == null || characterSelectPanel == null || roomGraphController == null)
                throw new InvalidOperationException("OutGameFlowController의 필드가 배선되지 않았습니다.");

            // MainMenu를 거치지 않고 OutGame.unity가 곧장 열리는 경로(테스트 등)에 대비한 방어적 적용
            // (MainMenuController.Awake()와 동일 이유).
            AudioListener.volume = SoundSettings.MasterVolume;

            mapSelectPanel.MapConfirmed += OnMapConfirmed;
            characterSelectPanel.CharacterConfirmed += OnCharacterConfirmed;
            characterSelectPanel.BackRequested += OnCharacterSelectBack;
        }

        private void Start()
        {
            // 이후 검증에서 예외가 나더라도 PendingRun이 stale 상태로 남지 않도록 가장 먼저 소비한다.
            RunState pendingRun = RunSessionContext.ConsumePendingRun();

            if (pendingRun != null)
            {
                // 씬 교체 전투(§7.4) 복귀 — 맵/캐릭터 선택이 이미 끝난 런. 2026-10-19 부활: 이 지점에서
                // "의지의 파편" 오버레이를 한 번 거친 뒤 방 그래프로 진입한다. 단, 파편이 걸리지 않으면
                // (≈85%) 오버레이는 LoreFragmentOverlayPanel.Begin 내부에서 즉시 통과하므로 끊김이 없다
                // (2026-08-10 폐기 원인이던 "빈 화면 클릭 강제"를 그렇게 해소했다).
                void EnterRoomGraph()
                {
                    ShowOnly(roomGraphController.gameObject);
                    roomGraphController.Begin(pendingRun);
                }

                if (loreOverlay != null)
                    loreOverlay.Begin(EnterRoomGraph);
                else
                {
                    Debug.LogWarning("[OutGameFlowController] loreOverlay 미배선 — 의지의 파편 오버레이를 건너뛰고 방 그래프로 직행합니다.");
                    EnterRoomGraph();
                }
            }
            else
            {
                ShowOnly(mapSelectPanel.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (mapSelectPanel != null) mapSelectPanel.MapConfirmed -= OnMapConfirmed;
            if (characterSelectPanel != null)
            {
                characterSelectPanel.CharacterConfirmed -= OnCharacterConfirmed;
                characterSelectPanel.BackRequested -= OnCharacterSelectBack;
            }
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

        /// <summary>캐릭터 선택의 [뒤로] — 맵 선택 패널로 되돌린다. 씬을 다시 로드하지 않으므로
        /// MapSelectPanel.Awake()는 재실행되지 않고 이미 배치된 지점들이 그대로 남아 있다.</summary>
        private void OnCharacterSelectBack()
        {
            ShowOnly(mapSelectPanel.gameObject);
        }

        private void ShowOnly(GameObject target)
        {
            mapSelectPanel.gameObject.SetActive(ReferenceEquals(mapSelectPanel.gameObject, target));
            characterSelectPanel.gameObject.SetActive(ReferenceEquals(characterSelectPanel.gameObject, target));
            roomGraphController.gameObject.SetActive(ReferenceEquals(roomGraphController.gameObject, target));
        }
    }
}
