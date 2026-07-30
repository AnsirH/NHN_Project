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
    ///
    /// 아웃게임/인게임이 서로 다른 씬일 때(2026-07-26)는 <see cref="Implementation"/> 안에서
    /// <see cref="SetPendingBattle"/>로 setup/콜백을 보관해두고 SceneManager.LoadScene을 호출하면 된다 —
    /// static 필드는 씬 로드를 거쳐도 살아있으므로(도메인 리로드가 아닌 한) 새 씬에서
    /// <see cref="ConsumePendingSetup"/>으로 그대로 꺼내 읽을 수 있다. 이 클래스는 일부러
    /// UnityEngine.SceneManagement를 참조하지 않는다 — EditMode 테스트 가능성을 유지하기 위해,
    /// 실제 SceneManager.LoadScene 호출은 항상 이 클래스를 쓰는 MonoBehaviour 쪽 책임이다.
    /// </summary>
    public static class BattleBridge
    {
        private static Action<BattleSetupData, Action<BattleResultData>> implementation = DefaultImplementation;
        private static BattleSetupData pendingSetup;
        private static Action<BattleResultData> pendingCallback;
        private static BattleResultData pendingResult;

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

        /// <summary>
        /// 인게임 씬으로 전환하기 직전에 setup/콜백을 보관한다 — 새 씬의 부트스트랩 스크립트가
        /// <see cref="ConsumePendingSetup"/>으로 이어받는다.
        /// </summary>
        public static void SetPendingBattle(BattleSetupData setup, Action<BattleResultData> onResult)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            pendingSetup = setup;
            pendingCallback = onResult;
        }

        /// <summary>
        /// 인게임 씬이 로드된 뒤 부트스트랩 스크립트가 호출 — 보관된 BattleSetupData를 꺼내가며 즉시
        /// 비운다(다음 전투와 섞이는 것 방지). 보관된 게 없으면(씬을 단독으로 열어 테스트하는 경우 등) null.
        /// </summary>
        public static BattleSetupData ConsumePendingSetup()
        {
            BattleSetupData setup = pendingSetup;
            pendingSetup = null;
            return setup;
        }

        /// <summary>
        /// 인게임이 전투를 끝냈을 때 호출 — 보관해둔 콜백을 실행하고 비운다. 콜백 실행 후 아웃게임
        /// 씬으로 되돌아가는 것은 호출한 쪽(인게임 부트스트랩)의 책임이다.
        /// </summary>
        public static void CompleteBattle(BattleResultData result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            Action<BattleResultData> callback = pendingCallback;
            pendingCallback = null;

            if (callback == null)
                throw new InvalidOperationException(
                    "CompleteBattle이 호출됐지만 대기 중인 콜백이 없습니다 — SetPendingBattle 없이 호출됐거나 이미 완료 처리된 전투입니다.");

            callback(result);
        }

        /// <summary>
        /// 씬 교체 방식 전투의 복귀 경로 (§7.4, 2026-07-30): 아웃게임 씬이 이미 파괴돼
        /// <see cref="CompleteBattle"/>의 콜백을 부를 수 없을 때, 인게임이 결과를 여기 보관하고
        /// 아웃게임 씬을 다시 로드한다 — 복귀한 아웃게임(InGameFlowController.Begin)이
        /// <see cref="ConsumePendingResult"/>로 이어받아 기존 결과 처리 경로를 그대로 탄다.
        /// </summary>
        public static void SetPendingResult(BattleResultData result)
        {
            pendingResult = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>보관된 전투 결과를 꺼내며 즉시 비운다(다음 전투와 섞이는 것 방지) — 없으면 null(일반 진입).</summary>
        public static BattleResultData ConsumePendingResult()
        {
            BattleResultData result = pendingResult;
            pendingResult = null;
            return result;
        }

        /// <summary>Implementation과 보관된 대기 상태를 전부 기본값으로 되돌린다 — 테스트 간 정적 상태 격리용.</summary>
        public static void ResetToDefault()
        {
            Implementation = DefaultImplementation;
            pendingSetup = null;
            pendingCallback = null;
            pendingResult = null;
        }

        private static void DefaultImplementation(BattleSetupData setup, Action<BattleResultData> onResult) =>
            throw new InvalidOperationException(
                "BattleBridge.Implementation이 설정되지 않았습니다 — 더미 전투 패널 또는 인게임 연결이 필요합니다.");
    }
}
