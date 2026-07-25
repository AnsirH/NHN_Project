using System.IO;
using NHN.Data;
using NHN.Simulation.Balance;
using NHN.Simulation.Battle;
using NUnit.Framework;
using UnityEditor;

namespace NHN.Simulation.Tests
{
    /// <summary>
    /// 스탯 CSV 파이프라인 검증 (5.6-C): 전개 테이블 파싱의 fail-fast + 실제 CSV와 .asset 레벨 0의 일치.
    /// 파서는 Simulation의 StatTable 하나이며 에디터 임포터와 BalanceLab CLI가 이를 공유한다.
    /// </summary>
    public sealed class StatTableTests
    {
        private const string SoldierCsv = "Assets/Data/Csv/soldier_stats.csv";
        private const string GeneralCsv = "Assets/Data/Csv/general_stats.csv";

        private const string ValidCsv =
            "# 주석\n" +
            "classId,level,maxHp,attackDamage,defense,critChancePercent,moveSpeed\n" +
            "Warrior,0,100,12,0,0,3.5\n" +
            "Warrior,1,112,13,8,1,3.5\n";

        [Test]
        public void Parse_ReadsRowsAndSkipsComments()
        {
            StatTable table = StatTable.Parse(ValidCsv, "test.csv");

            Assert.AreEqual(1, table.ClassIds.Count);
            Assert.AreEqual("Warrior", table.ClassIds[0]);
            Assert.AreEqual(1, table.MaxLevel);

            StatTable.StatRow level1 = table.Get("Warrior", 1);
            Assert.AreEqual(112f, level1.MaxHp, 1e-3f);
            Assert.AreEqual(13f, level1.AttackDamage, 1e-3f);
            Assert.AreEqual(8f, level1.Defense, 1e-3f);
            Assert.AreEqual(1f, level1.CritChancePercent, 1e-3f);
            Assert.AreEqual(3.5f, level1.MoveSpeed, 1e-3f);
        }

        [Test]
        public void Parse_FailsFast_OnMalformedInput()
        {
            // 헤더 불일치
            Assert.Throws<System.FormatException>(() => StatTable.Parse(
                "classId,level,maxHp\nWarrior,0,100\n", "bad_header.csv"));

            // 열 개수 부족
            Assert.Throws<System.FormatException>(() => StatTable.Parse(
                StatTable.ExpectedHeader + "\nWarrior,0,100,12,0,0\n", "short_row.csv"));

            // 수치 파싱 실패
            Assert.Throws<System.FormatException>(() => StatTable.Parse(
                StatTable.ExpectedHeader + "\nWarrior,0,백,12,0,0,3.5\n", "bad_number.csv"));

            // 중복 행
            Assert.Throws<System.FormatException>(() => StatTable.Parse(
                StatTable.ExpectedHeader + "\nWarrior,0,100,12,0,0,3.5\nWarrior,0,101,12,0,0,3.5\n", "dup.csv"));

            // 레벨 누락 (0, 2만 있고 1이 없음)
            Assert.Throws<System.FormatException>(() => StatTable.Parse(
                StatTable.ExpectedHeader + "\nWarrior,0,100,12,0,0,3.5\nWarrior,2,120,14,0,0,3.5\n", "gap.csv"));

            // 미등록 병과 조회
            StatTable table = StatTable.Parse(ValidCsv, "test.csv");
            Assert.Throws<System.FormatException>(() => table.Get("Cavalry", 0));
        }

        /// <summary>실제 CSV: 5병과 × 6레벨(0~5) 구조 + 병사/장군 병과 목록 일치.</summary>
        [Test]
        public void ProjectCsv_HasExpectedShape()
        {
            StatTable soldiers = StatTable.Parse(File.ReadAllText(SoldierCsv), "soldier_stats.csv");
            StatTable generals = StatTable.Parse(File.ReadAllText(GeneralCsv), "general_stats.csv");

            Assert.AreEqual(5, soldiers.MaxLevel, "레벨 축은 0~5 하나다 (분대 = 장군 = 강화 레벨)");
            Assert.AreEqual(5, generals.MaxLevel);
            Assert.AreEqual(5, soldiers.ClassIds.Count, "노멀 + 롤 4종");
            CollectionAssert.AreEquivalent(soldiers.ClassIds, generals.ClassIds,
                "장군 스탯도 병과별로 나뉘므로 두 표의 병과 목록이 같아야 한다");
        }

        /// <summary>CSV 레벨 0 = .asset 스탯 (임포트 상태 검증 — CLI 가드와 같은 규칙을 에디터에서도 확인).</summary>
        [Test]
        public void AssetStats_MatchCsvLevelZero()
        {
            StatTable soldiers = StatTable.Parse(File.ReadAllText(SoldierCsv), "soldier_stats.csv");
            StatTable generals = StatTable.Parse(File.ReadAllText(GeneralCsv), "general_stats.csv");

            for (int c = 0; c < soldiers.ClassIds.Count; c++)
            {
                string classId = soldiers.ClassIds[c];
                var role = AssetDatabase.LoadAssetAtPath<RoleData>($"Assets/Data/Roles/{classId}.asset");
                Assert.IsNotNull(role, $"{classId} RoleData 에셋이 있어야 한다");
                StatTable.StatRow row = soldiers.Get(classId, 0);
                Assert.AreEqual(row.MaxHp, role.MaxHp, 1e-3f, $"{classId}.maxHp");
                Assert.AreEqual(row.AttackDamage, role.AttackDamage, 1e-3f, $"{classId}.attackDamage");
                Assert.AreEqual(row.Defense, role.Defense, 1e-3f, $"{classId}.defense");
                Assert.AreEqual(row.CritChancePercent, role.CritChancePercent, 1e-3f, $"{classId}.critChancePercent");
                Assert.AreEqual(row.MoveSpeed, role.MoveSpeed, 1e-3f, $"{classId}.moveSpeed");

                string generalName = classId + "General";
                var general = AssetDatabase.LoadAssetAtPath<GeneralData>($"Assets/Data/Generals/{generalName}.asset");
                Assert.IsNotNull(general, $"{generalName} GeneralData 에셋이 있어야 한다");
                Assert.IsTrue(general.HasExplicitStats, $"{generalName}: CSV 임포트로 스탯이 채워져 있어야 한다");
                StatTable.StatRow generalRow = generals.Get(classId, 0);
                Assert.AreEqual(generalRow.MaxHp, general.MaxHp, 1e-3f, $"{generalName}.maxHp");
                Assert.AreEqual(generalRow.AttackDamage, general.AttackDamage, 1e-3f, $"{generalName}.attackDamage");
                Assert.AreEqual(generalRow.MoveSpeed, general.MoveSpeed, 1e-3f, $"{generalName}.moveSpeed");
            }
        }

        /// <summary>노멀 장군: 능력 없이 스탯만 높은 장군 (아이템 미부여 분대용 — 회의 확정).</summary>
        [Test]
        public void NormalGeneral_HasNoAbilities_ButHigherStats()
        {
            var normalGeneral = AssetDatabase.LoadAssetAtPath<GeneralData>("Assets/Data/Generals/NormalGeneral.asset");
            var normalRole = AssetDatabase.LoadAssetAtPath<RoleData>("Assets/Data/Roles/Normal.asset");
            Assert.IsNotNull(normalGeneral);
            Assert.IsNotNull(normalRole);

            GeneralDefinition definition = normalGeneral.ToDefinition();
            Assert.AreEqual(SquadPassive.None, definition.Passive, "노멀 장군은 패시브가 없어야 한다");
            Assert.AreEqual(ChargeCondition.None, definition.ChargeCondition, "충전 조건이 없어야 한다");
            Assert.AreEqual(GimmickEffect.None, definition.ActiveEffect, "액티브 효과가 없어야 한다");
            Assert.Greater(definition.CombatRole.MaxHp, normalRole.MaxHp, "스탯은 병사보다 높아야 한다");
            Assert.Greater(definition.CombatRole.UnitRadius, normalRole.UnitRadius, "크기 배율은 유지된다 (즉시 구분)");
        }
    }
}
