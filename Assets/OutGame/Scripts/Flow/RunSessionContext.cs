using System;
using OutGame.Logic.Runs;

namespace OutGame.Flow
{
    /// <summary>
    /// 씬 전환 간 전달되는 런 상태 보관소 (§3.1 씬 통합 이후 딱 두 경계에서만 쓰인다):
    /// (1) 메인메뉴 "이어하기" → OutGame.unity 진입("게임 시작"은 새 런이라 안 씀).
    /// (2) OutGame → 실제 전투 씬 경계는 별도 메커니즘(BattleBridge, §7.4)이 담당 — 이 클래스는
    /// 관여하지 않는다. 맵 선택↔캐릭터 선택↔방 그래프 사이의 내부 전환(OutGame.unity 안)은 더 이상
    /// 이 클래스를 쓰지 않고 로컬 RunState 참조를 이벤트 인자로 직접 넘긴다(OutGameFlowController 참고).
    /// MonoBehaviour가 파괴되는 씬 전환 특성상, 정적 필드로 다음 씬의 Start()까지 값을 넘긴다.
    /// </summary>
    public static class RunSessionContext
    {
        public static RunState PendingRun { get; private set; }

        public static void SetPendingRun(RunState run) =>
            PendingRun = run ?? throw new ArgumentNullException(nameof(run));

        /// <summary>보관된 런을 반환하고 비운다 (한 번 소비하면 다음 진입 시 재사용되지 않도록).</summary>
        public static RunState ConsumePendingRun()
        {
            RunState run = PendingRun;
            PendingRun = null;
            return run;
        }
    }
}
