using System;
using UnityEngine;

namespace OutGame.UI
{
    /// <summary>
    /// 팝업의 dim 배경을 팝업 자신의 표시/숨김에 맞춰 자동으로 켜고 끈다(2026-07-26) — 팝업 쪽
    /// GameObject에 붙이면, 각 팝업이 Open()/Close()마다 dim을 따로 챙기지 않아도 OnEnable/OnDisable로
    /// 알아서 따라간다. dimEnabled를 인스펙터에서 false로 두면 인벤토리/군대 정보 팝업처럼 dim 없는
    /// 팝업도 같은 프리팹 패턴으로 만들 수 있다 — 게임 안의 많은 팝업이 "dim(옵션) + 창" 구조를 각자
    /// 손으로 만들고 있어 재사용 가능한 조각으로 뽑음(사용자 확정). 새 팝업부터 적용하고, 기존
    /// 팝업들은 점진적으로 옮긴다.
    /// </summary>
    public class PopupDim : MonoBehaviour
    {
        [SerializeField] private GameObject dimBackground;
        [SerializeField] private bool dimEnabled = true;

        private void Awake()
        {
            if (dimBackground == null)
                throw new InvalidOperationException("PopupDim의 dimBackground가 배선되지 않았습니다.");
        }

        // Awake()가 배선 누락을 이미 명확한 예외로 알렸으므로, 여기서는 null 체크만 하고 조용히
        // 넘어간다 — 안 그러면 같은 활성화 패스 안에서 Awake() 예외 바로 뒤에 이 NRE까지 겹쳐서
        // 진짜 원인(dimBackground 미배선)이 무엇인지 헷갈리는 두 번째 예외가 뜬다(코드 리뷰 지적).
        private void OnEnable()
        {
            if (dimBackground != null) dimBackground.SetActive(dimEnabled);
        }

        private void OnDisable()
        {
            if (dimBackground != null) dimBackground.SetActive(false);
        }
    }
}
