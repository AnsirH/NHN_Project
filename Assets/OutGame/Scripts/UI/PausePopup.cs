using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 일시정지 패널 (2026-07-31, 아웃게임 전용) — ESC/안드로이드 뒤로가기로 토글해서 열고 닫는다.
    /// 새 Input System(이 프로젝트는 activeInputHandler=1, 레거시 UnityEngine.Input 사용 불가)에서는
    /// 안드로이드 뒤로가기도 Keyboard.escapeKey로 들어온다.
    /// 버튼은 "메인메뉴로 돌아가기"(SceneLoadButton 재사용)와 "환경 설정"(SettingsPopup을 그 위에
    /// 띄움) 둘뿐이다 — 게임으로 복귀하는 유일한 방법은 ESC/뒤로가기를 다시 누르는 것(사용자 확정).
    /// <see cref="panelRoot"/>(dim+창+버튼)만 토글하고 이 컴포넌트가 붙은 오브젝트 자신은 항상 활성
    /// 상태를 유지한다 — Unity는 비활성 GameObject의 Update()를 호출하지 않으므로, 패널이 닫혀 있는
    /// 동안에도 ESC 입력을 계속 감지하려면 폴링 주체가 별도로 항상 켜져 있어야 한다.
    /// </summary>
    public class PausePopup : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPopup settingsPopup;
        // 2026-08-10: 화면상 닫기 경로. 원래는 ESC/뒤로가기만으로 닫게 뒀는데, 방 그래프 상단 바에
        // 이 팝업을 여는 버튼이 생기면서 키 없이 닫을 방법도 필요해졌다(사용자 확정).
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (panelRoot == null || settingsButton == null || settingsPopup == null || closeButton == null)
                throw new InvalidOperationException("PausePopup의 필드가 배선되지 않았습니다.");

            settingsButton.onClick.AddListener(OnSettingsClicked);
            closeButton.onClick.AddListener(Hide);
            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            closeButton.onClick.RemoveListener(Hide);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Toggle();
        }

        public void Toggle()
        {
            if (panelRoot.activeSelf) Hide();
            else Show();
        }

        public void Show()
        {
            panelRoot.SetActive(true);
        }

        public void Hide() => panelRoot.SetActive(false);

        private void OnSettingsClicked() => settingsPopup.Show();
    }
}
