using NHN.Simulation.Balance;

namespace NHN.EditorTools
{
    /// <summary>
    /// 스탯 CSV 파이프라인의 공통 규약 — 임포터·익스포터·BalanceLab 가드가 같은 경로/이름 규칙을 쓴다.
    ///
    /// 정본 흐름: 구글 시트(원본) → CSV(커밋) ─┬→ 아웃게임: 스탯 계산 → 전투 시 전달
    ///                                        └→ 인게임 .asset: 임포트(레벨 0 스냅샷) = 로컬 경로 기본값
    /// AI 튜닝은 .asset을 고치므로, 확정된 튜닝 결과는 익스포터로 CSV에 되돌려 시트에 반영해야 한다.
    /// </summary>
    public static class StatCsvPaths
    {
        public const string SoldierCsv = "Assets/Data/Csv/soldier_stats.csv";
        public const string GeneralCsv = "Assets/Data/Csv/general_stats.csv";

        /// <summary>임포터·익스포터가 스냅샷으로 다루는 레벨 (인게임 .asset은 한 벌만 담는다).</summary>
        public const int SnapshotLevel = 0;

        /// <summary>병과 id → RoleData 에셋 이름 (동일).</summary>
        public static string RoleAssetName(string classId) => classId;

        /// <summary>병과 id → GeneralData 에셋 이름 규약 (예: Warrior → WarriorGeneral). 규약은 StatTable이 소유 — CLI와 공유.</summary>
        public static string GeneralAssetName(string classId) => StatTable.GeneralAssetName(classId);
    }
}
