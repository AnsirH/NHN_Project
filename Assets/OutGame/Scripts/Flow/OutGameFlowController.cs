using System;
using OutGame.Logic.Runs;
using UnityEngine;

namespace OutGame.Flow
{
    /// <summary>
    /// OutGame.unity 루트 코디네이터 (§3.1 씬 통합): 맵 선택 → 캐릭터 선택 → 방 그래프 세 패널을
    /// 활성화 토글로 전환시킨다. 이전에는 셋이 각자 씬이라 RunSessionContext(정적 필드)로 넘겼지만,
    /// 이제 같은 씬 안의 형제 GameObject라 확정된 RunState를 이벤트 인자로 직접 전달한다.
    /// RunSessionContext는 "메인메뉴→아웃게임" 경계(이어하기)에서만 여전히 쓰인다 — 그 소비 지점이
    /// 바로 이 클래스의 Start().
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

            mapSelectPanel.MapConfirmed += OnMapConfirmed;
            characterSelectPanel.CharacterConfirmed += OnCharacterConfirmed;
        }

        private void Start()
        {
            // 이후 검증에서 예외가 나더라도 PendingRun이 stale 상태로 남지 않도록 가장 먼저 소비한다.
            RunState pendingRun = RunSessionContext.ConsumePendingRun();

            if (pendingRun != null)
            {
                // "이어하기" — 맵/캐릭터 선택이 이미 끝난 런이라 방 그래프로 곧장 진입한다.
                ShowOnly(roomGraphController.gameObject);
                roomGraphController.Begin(pendingRun);
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
