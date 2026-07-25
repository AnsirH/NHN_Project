using System;
using System.Collections.Generic;
using System.Globalization;

namespace NHN.Simulation.Balance
{
    /// <summary>
    /// 병과 × 레벨 스탯 전개 테이블 (soldier_stats.csv / general_stats.csv의 파싱 결과).
    ///
    /// 이 클래스가 Simulation(순수 C#)에 있는 이유: Unity 에디터 임포터와 BalanceLab CLI가
    /// **같은 파서 하나**를 공유해야 하기 때문이다 (파서가 둘이면 두 쪽 해석이 조용히 갈라진다).
    /// 파일 I/O는 하지 않는다 — 호출자가 읽은 텍스트를 넘긴다.
    ///
    /// 계산식이 없는 전개 테이블인 것이 핵심: 아웃게임과 인게임이 각자 공식을 구현하면
    /// 반올림·적용 순서 차이로 수치가 어긋나는데, 값을 그대로 적으면 어긋날 여지가 없다.
    /// </summary>
    public sealed class StatTable
    {
        /// <summary>한 (병과, 레벨)의 5스탯. CritChancePercent는 0~100 퍼센트 단위.</summary>
        public readonly struct StatRow
        {
            public readonly float MaxHp;
            public readonly float AttackDamage;
            public readonly float Defense;
            public readonly float CritChancePercent;
            public readonly float MoveSpeed;

            public StatRow(float maxHp, float attackDamage, float defense, float critChancePercent, float moveSpeed)
            {
                MaxHp = maxHp;
                AttackDamage = attackDamage;
                Defense = defense;
                CritChancePercent = critChancePercent;
                MoveSpeed = moveSpeed;
            }
        }

        public const string ExpectedHeader = "classId,level,maxHp,attackDamage,defense,critChancePercent,moveSpeed";
        private const int ColumnCount = 7;

        private readonly Dictionary<string, StatRow[]> _rowsByClass;
        private readonly List<string> _classIds;

        public string SourceName { get; }

        /// <summary>테이블이 담고 있는 최대 레벨 (레벨은 0..MaxLevel 연속이어야 한다).</summary>
        public int MaxLevel { get; }

        public IReadOnlyList<string> ClassIds => _classIds;

        private StatTable(string sourceName, Dictionary<string, StatRow[]> rowsByClass, List<string> classIds, int maxLevel)
        {
            SourceName = sourceName;
            _rowsByClass = rowsByClass;
            _classIds = classIds;
            MaxLevel = maxLevel;
        }

        public bool TryGet(string classId, int level, out StatRow row)
        {
            if (classId != null && _rowsByClass.TryGetValue(classId, out StatRow[] levels)
                && level >= 0 && level < levels.Length)
            {
                row = levels[level];
                return true;
            }
            row = default;
            return false;
        }

        /// <summary>미등록 병과·레벨은 즉시 실패 — 측정 도구의 폴백은 밸런싱을 가짜로 만든다.</summary>
        public StatRow Get(string classId, int level)
        {
            if (!TryGet(classId, level, out StatRow row))
            {
                throw new FormatException(
                    $"{SourceName}: 병과 '{classId}' 레벨 {level}의 행이 없다 (레벨 범위 0~{MaxLevel})");
            }
            return row;
        }

        /// <summary>
        /// CSV 텍스트를 파싱한다. '#'로 시작하는 줄과 빈 줄은 주석으로 무시한다.
        /// 헤더 불일치·열 개수 오류·수치 파싱 실패·중복 행·레벨 누락은 전부 즉시 예외 (fail-fast).
        /// </summary>
        public static StatTable Parse(string csvText, string sourceName)
        {
            if (csvText == null)
            {
                throw new ArgumentNullException(nameof(csvText));
            }

            string[] lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var rawByClass = new Dictionary<string, Dictionary<int, StatRow>>();
            var classOrder = new List<string>();
            bool headerSeen = false;
            int maxLevel = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (!headerSeen)
                {
                    if (line.Replace(" ", string.Empty) != ExpectedHeader)
                    {
                        throw new FormatException(
                            $"{sourceName}:{i + 1} 헤더가 다르다.\n  기대: {ExpectedHeader}\n  실제: {line}");
                    }
                    headerSeen = true;
                    continue;
                }

                string[] cells = line.Split(',');
                if (cells.Length != ColumnCount)
                {
                    throw new FormatException(
                        $"{sourceName}:{i + 1} 열 개수가 {cells.Length}개다 (기대 {ColumnCount}개): {line}");
                }

                string classId = cells[0].Trim();
                if (classId.Length == 0)
                {
                    throw new FormatException($"{sourceName}:{i + 1} classId가 비어 있다");
                }
                int level = ParseInt(cells[1], sourceName, i + 1, "level");
                if (level < 0)
                {
                    throw new FormatException($"{sourceName}:{i + 1} level은 0 이상이어야 한다 (현재 {level})");
                }

                var row = new StatRow(
                    ParseFloat(cells[2], sourceName, i + 1, "maxHp"),
                    ParseFloat(cells[3], sourceName, i + 1, "attackDamage"),
                    ParseFloat(cells[4], sourceName, i + 1, "defense"),
                    ParseFloat(cells[5], sourceName, i + 1, "critChancePercent"),
                    ParseFloat(cells[6], sourceName, i + 1, "moveSpeed"));
                if (row.MaxHp <= 0f)
                {
                    throw new FormatException($"{sourceName}:{i + 1} maxHp는 0보다 커야 한다 ({classId} 레벨 {level})");
                }

                if (!rawByClass.TryGetValue(classId, out Dictionary<int, StatRow> levels))
                {
                    levels = new Dictionary<int, StatRow>();
                    rawByClass.Add(classId, levels);
                    classOrder.Add(classId);
                }
                if (levels.ContainsKey(level))
                {
                    throw new FormatException($"{sourceName}:{i + 1} ({classId}, 레벨 {level}) 행이 중복이다");
                }
                levels.Add(level, row);
                maxLevel = Math.Max(maxLevel, level);
            }

            if (!headerSeen || classOrder.Count == 0)
            {
                throw new FormatException($"{sourceName}: 데이터 행이 없다");
            }

            // 모든 병과가 레벨 0..maxLevel을 빠짐없이 가져야 한다 — 누락은 조용한 폴백 대신 실패로 드러낸다.
            var rowsByClass = new Dictionary<string, StatRow[]>(classOrder.Count);
            for (int c = 0; c < classOrder.Count; c++)
            {
                string classId = classOrder[c];
                Dictionary<int, StatRow> levels = rawByClass[classId];
                var packed = new StatRow[maxLevel + 1];
                for (int level = 0; level <= maxLevel; level++)
                {
                    if (!levels.TryGetValue(level, out StatRow row))
                    {
                        throw new FormatException($"{sourceName}: 병과 '{classId}'에 레벨 {level} 행이 없다");
                    }
                    packed[level] = row;
                }
                rowsByClass.Add(classId, packed);
            }

            return new StatTable(sourceName, rowsByClass, classOrder, maxLevel);
        }

        /// <summary>익스포트·비교 로그용 수치 표기 — 로캘 무관 고정 형식.</summary>
        public static string FormatValue(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string cell, string sourceName, int lineNumber, string column)
        {
            if (!int.TryParse(cell.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new FormatException($"{sourceName}:{lineNumber} '{column}' 값 '{cell}'을 정수로 해석할 수 없다");
            }
            return value;
        }

        private static float ParseFloat(string cell, string sourceName, int lineNumber, string column)
        {
            if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                throw new FormatException($"{sourceName}:{lineNumber} '{column}' 값 '{cell}'을 실수로 해석할 수 없다");
            }
            return value;
        }
    }
}
