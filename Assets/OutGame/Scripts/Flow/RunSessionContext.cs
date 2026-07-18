using System;
using OutGame.Logic.Runs;

namespace OutGame.Flow
{
    /// <summary>
    /// 씬 전환 간 전달되는 런 상태 보관소 (메인 메뉴/맵 선택 → 인게임).
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
