using System;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>재화(골드) 표시 — 아이콘 + 숫자. 방 그래프 패널/배치 패널에서 공용으로 재사용한다(2026-07-26).</summary>
    public class CurrencyDisplay : MonoBehaviour
    {
        [SerializeField] private Text amountLabel;

        // Awake()가 아니라 여기서 검증한다 — ArmyDeploymentPanel.Open()은 아직 비활성 상태에서
        // RefreshLayout()(SetAmount 호출 포함)을 먼저 실행하는데, Unity는 비활성 GameObject의
        // Awake()를 활성화 시점까지 미루므로 Awake() 쪽 검증은 첫 오픈 때 아직 실행되지 않은 채
        // 지나간다(ArmyInfoPopup 업그레이드 버튼 라벨과 동일한 문제, 2026-07-26 코드 리뷰).
        public void SetAmount(int amount)
        {
            if (amountLabel == null)
                throw new InvalidOperationException("CurrencyDisplay 프리팹의 amountLabel이 배선되지 않았습니다.");
            amountLabel.text = amount.ToString();
        }
    }
}
