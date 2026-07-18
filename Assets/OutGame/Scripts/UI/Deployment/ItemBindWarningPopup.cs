using System;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 아이템 귀속 경고 팝업 (상세 기획 §5.6 — 기획서 필수 요구사항).
    /// "다시 표시하지 않음" 체크 시 PlayerPrefs에 영구 저장 (§4-12).
    /// </summary>
    public class ItemBindWarningPopup : MonoBehaviour
    {
        public const string SuppressPrefKey = "OutGame.ItemBindWarning.Suppressed";

        [SerializeField] private Text messageLabel;
        [SerializeField] private Toggle suppressToggle;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action onConfirmed;

        private void Awake()
        {
            if (messageLabel == null || suppressToggle == null || confirmButton == null || cancelButton == null)
                throw new InvalidOperationException("ItemBindWarningPopup 프리팹의 필드가 배선되지 않았습니다.");

            confirmButton.onClick.AddListener(OnConfirmClicked);
            cancelButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            cancelButton.onClick.RemoveListener(Hide);
        }

        public static bool IsSuppressed => PlayerPrefs.GetInt(SuppressPrefKey, 0) == 1;

        /// <summary>팝업이 이미 확인 대기 중인지 — 호출부가 새 드롭 요청을 무시할지 판단하는 데 사용.</summary>
        public bool IsShowing => gameObject.activeSelf;

        /// <summary>
        /// 팝업을 띄우거나(사용자가 다시 표시 안 함을 켜지 않았다면), 이미 억제됐으면
        /// 팝업 없이 즉시 onConfirmed를 호출한다 (§5.6: 체크 시 팝업 생략하고 즉시 부여).
        /// 이미 확인 대기 중이면(IsShowing) 기존 요청을 덮어쓰지 않고 무시한다.
        /// </summary>
        public void ShowOrConfirmImmediately(string itemName, string armyName, Action onConfirm)
        {
            if (onConfirm == null) throw new ArgumentNullException(nameof(onConfirm));
            if (IsShowing) return;

            if (IsSuppressed)
            {
                onConfirm();
                return;
            }

            onConfirmed = onConfirm;
            messageLabel.text = $"'{itemName}'을(를) '{armyName}'에 부여합니다.\n" +
                                 "이 아이템은 부대에 귀속되며 되돌릴 수 없습니다.";
            suppressToggle.isOn = false;
            transform.SetAsLastSibling(); // 인벤토리 팝업이 이미 열려 있어도 항상 그 위에 떠야 함 (버그 수정)
            gameObject.SetActive(true);
        }

        private void OnConfirmClicked()
        {
            if (suppressToggle.isOn)
                PlayerPrefs.SetInt(SuppressPrefKey, 1);

            Action confirm = onConfirmed;
            Hide();
            confirm?.Invoke();
        }

        private void Hide()
        {
            onConfirmed = null;
            gameObject.SetActive(false);
        }
    }
}
