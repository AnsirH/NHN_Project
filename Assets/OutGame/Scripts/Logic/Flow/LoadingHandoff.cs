using System;

namespace OutGame.Flow
{
    /// <summary>
    /// OutGame → Loading → Battle 씬 전환 시, Loading 씬이 "다음에 뭘 로드해야 하는지"를 넘겨받는
    /// 통로 (2026-08-04). BattleBridge/RunSessionContext와 동일한 static 홀더 패턴 — MonoBehaviour가
    /// 파괴되는 씬 전환 특성상 정적 필드로 값을 넘긴다. 일부러 UnityEngine을 참조하지 않는다
    /// (EditMode 테스트 가능성 유지, BattleBridge와 동일한 이유).
    /// </summary>
    public static class LoadingHandoff
    {
        private static string targetScene;

        public static void SetTarget(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("sceneName이 비어 있습니다.", nameof(sceneName));

            targetScene = sceneName;
        }

        /// <summary>보관된 대상 씬 이름을 반환하고 비운다(한 번 소비하면 다음 진입 시 재사용되지 않도록).</summary>
        public static string ConsumeTarget()
        {
            string scene = targetScene;
            targetScene = null;
            return scene;
        }

        /// <summary>보관된 상태를 기본값으로 되돌린다 — 테스트 간 정적 상태 격리용.</summary>
        public static void ResetToDefault() => targetScene = null;
    }
}
