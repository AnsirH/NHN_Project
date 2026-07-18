using System;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 아웃게임 → 인게임 호출 지점 (상세 기획 §7.3 — 인터페이스 계약).
    /// <see cref="Implementation"/>은 교체 가능한 정적 델리게이트다 — M6에서는 더미 UI가 결과를 채워 넣고,
    /// 이후 인게임 개발자가 실제 전투 로직으로 교체할 때 이 지점 외에는 아무것도 손댈 필요가 없다.
    /// 씬이 다시 로드될 때마다(Domain Reload가 꺼져 있어도) 유효한 대상을 가리키도록,
    /// 이 델리게이트를 설정하는 쪽(예: InGameFlowController.Start())이 매번 재등록해야 한다 —
    /// 파괴된 오브젝트의 클로저가 남아있으면 안 된다.
    /// </summary>
    public static class BattleBridge
    {
        private static Action<BattleSetupData, Action<BattleResultData>> implementation = DefaultImplementation;

        public static Action<BattleSetupData, Action<BattleResultData>> Implementation
        {
            get => implementation;
            set => implementation = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static void StartBattle(BattleSetupData setup, Action<BattleResultData> onResult)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            Implementation(setup, onResult);
        }

        /// <summary>Implementation을 기본값(미설정 상태)으로 되돌린다 — 테스트 간 정적 상태 격리용.</summary>
        public static void ResetToDefault() => Implementation = DefaultImplementation;

        private static void DefaultImplementation(BattleSetupData setup, Action<BattleResultData> onResult) =>
            throw new InvalidOperationException(
                "BattleBridge.Implementation이 설정되지 않았습니다 — 더미 전투 패널 또는 인게임 연결이 필요합니다.");
    }
}
