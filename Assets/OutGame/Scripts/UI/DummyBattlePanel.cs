using System;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// M6 더미 전투 패널 — BattleBridge.Implementation의 임시 구현체.
    /// 실제 전투 연출/판정 없이 [승리]/[패배] 버튼으로 결과를 즉시 확정해 통합 테스트를 가능하게 한다.
    /// 인게임 개발자가 실제 전투 로직을 연결할 때는 BattleBridge.Implementation을 교체하면 되고,
    /// 이 패널은 더 이상 쓰이지 않게 된다(호출부를 고칠 필요 없음, §7.3).
    /// </summary>
    public class DummyBattlePanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button victoryButton;
        [SerializeField] private Button defeatButton;

        private BattleSetupData currentSetup;
        private Action<BattleResultData> onResult;

        private void Awake()
        {
            if (titleText == null || bodyText == null || victoryButton == null || defeatButton == null)
                throw new InvalidOperationException(
                    "DummyBattlePanel의 titleText/bodyText/victoryButton/defeatButton이 배선되지 않았습니다.");

            victoryButton.onClick.AddListener(OnVictoryClicked);
            defeatButton.onClick.AddListener(OnDefeatClicked);
        }

        private void OnDestroy()
        {
            victoryButton.onClick.RemoveListener(OnVictoryClicked);
            defeatButton.onClick.RemoveListener(OnDefeatClicked);
        }

        private void OnVictoryClicked() => Resolve(victory: true);
        private void OnDefeatClicked() => Resolve(victory: false);

        /// <summary>BattleBridge.Implementation에 그대로 대입할 수 있는 시그니처.</summary>
        public void Open(BattleSetupData setup, Action<BattleResultData> onResultCallback)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (onResultCallback == null) throw new ArgumentNullException(nameof(onResultCallback));

            currentSetup = setup;
            onResult = onResultCallback;

            titleText.text = setup.roomType == RoomType.Boss ? "보스 전투 (더미)" : "전투 (더미)";
            bodyText.text = $"{setup.roomId} — 배치 부대 {setup.armies.Count}개\n(실제 전투는 인게임 연결 후 대체됨 — 결과를 선택하세요)";

            gameObject.SetActive(true);
            PanelTransitions.FadeIn(gameObject);
        }

        private void Resolve(bool victory)
        {
            Action<BattleResultData> callback = onResult;
            BattleSetupData setup = currentSetup;
            onResult = null;
            currentSetup = null;

            gameObject.SetActive(false);
            callback(new BattleResultData { roomId = setup.roomId, victory = victory });
        }
    }
}
