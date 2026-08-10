using System;
using System.Collections.Generic;
using System.IO;
using NHN.Simulation.Battle;

namespace BalanceLab
{
    public sealed class ReplayUnit
    {
        public int team;
        public int squad;
        public bool leader;
    }

    public sealed class ReplaySquad
    {
        public string id;
        public string roleId;
        public int team;
        public int soldiers;
        public bool hasGeneral;
    }

    /// <summary>타임라인 마커 — 샘플링에 먹히면 안 되는 정보라 정확한 틱을 그대로 보존한다.</summary>
    public sealed class ReplayEvent
    {
        /// <summary>"death" | "activation" | "squadWiped"</summary>
        public string type;
        public int tick;
        public int unit;
        public int squad;
        public int team;
    }

    public sealed class ReplayData
    {
        public string scenario;
        public string tag;
        public int seed;
        public string winner;
        public int ticks;
        public int tickRate;
        /// <summary>몇 틱마다 한 프레임을 담았는지. 뷰어는 프레임 사이를 보간해 재생한다.</summary>
        public int sampleEvery;
        public int frameCount;
        public int unitCount;
        /// <summary>좌표 양자화 배율 — 뷰어가 하드코딩하지 않도록 파일이 스스로 밝힌다.</summary>
        public int posScale;
        public float arenaHalfWidth;
        public float arenaHalfHeight;
        public List<ReplaySquad> squads;
        public List<ReplayUnit> units;
        /// <summary>프레임 블록 base64. 프레임 = [생존수 u16][생존수 × (유닛 u16, x i16, y i16, hp u8)], 전부 리틀엔디안.</summary>
        public string frames;
        public List<ReplayEvent> events;
    }

    /// <summary>
    /// 전투 궤적 기록기 — 시뮬 **바깥**에서 공개 접근자만 읽는다. 시뮬 코드는 이 기록기의 존재를 모른다.
    /// 그 덕에 "관측이 대상을 바꾸지 않는다"가 구조적으로 보장되고, 덤프 온/오프 회귀 테스트가 그것을 실측한다.
    ///
    /// 용량 설계:
    ///   · 10Hz 샘플링 — 30tps에서 3틱마다 1프레임 (뷰어가 보간)
    ///   · 좌표 int16 양자화 (1/100 단위 = 오차 1cm)
    ///   · 생존 유닛만 저장 — 전투가 진행될수록 프레임이 가벼워진다
    /// 사망·액티브 발동·분대 전멸은 매 틱 폴링해 정확한 틱으로 따로 남긴다.
    /// </summary>
    public sealed class ReplayRecorder
    {
        private const int TargetFramesPerSecond = 10;
        public const int PositionScale = 100;

        private readonly ReplayData _data;
        private readonly List<byte> _bytes = new List<byte>(1 << 16);
        private readonly bool[] _wasAlive;
        private readonly int[] _lastActivations;
        private readonly bool[] _squadWiped;
        private readonly int _sampleEvery;
        private int _tick;
        private int _lastSampledTick = -1;

        public ReplayRecorder(BattleSimulation sim, in BattleConfig config, string scenario, string tag, int seed)
        {
            _sampleEvery = Math.Max(1, (int)Math.Round(config.TicksPerSecond / (double)TargetFramesPerSecond));
            _wasAlive = new bool[sim.UnitCount];
            _lastActivations = new int[sim.SquadCount];
            _squadWiped = new bool[sim.SquadCount];

            var units = new List<ReplayUnit>(sim.UnitCount);
            for (int i = 0; i < sim.UnitCount; i++)
            {
                _wasAlive[i] = sim.IsAlive(i);
                units.Add(new ReplayUnit
                {
                    team = sim.GetTeam(i),
                    squad = sim.GetSquadIndex(i),
                    leader = sim.IsLeader(i),
                });
            }
            for (int s = 0; s < sim.SquadCount; s++)
            {
                _lastActivations[s] = sim.GetSquadActivationCount(s);
            }

            _data = new ReplayData
            {
                scenario = scenario,
                tag = string.IsNullOrEmpty(tag) ? null : tag,
                seed = seed,
                tickRate = config.TicksPerSecond,
                sampleEvery = _sampleEvery,
                unitCount = sim.UnitCount,
                posScale = PositionScale,
                arenaHalfWidth = config.ArenaHalfWidth,
                arenaHalfHeight = config.ArenaHalfHeight,
                units = units,
                squads = new List<ReplaySquad>(),
                events = new List<ReplayEvent>(),
            };

            CaptureFrame(sim); // 틱 0 = 초기 진형
        }

        public ReplayData Data => _data;

        public void DescribeSquads(List<SquadEntry> left, List<SquadEntry> right)
        {
            AddSquads(left, 0);
            AddSquads(right, 1);
        }

        private void AddSquads(List<SquadEntry> entries, int team)
        {
            foreach (SquadEntry entry in entries)
            {
                _data.squads.Add(new ReplaySquad
                {
                    id = entry.squadId,
                    roleId = string.IsNullOrEmpty(entry.roleId) ? "Normal" : entry.roleId,
                    team = team,
                    soldiers = entry.soldierCount,
                    hasGeneral = !string.IsNullOrEmpty(entry.generalId),
                });
            }
        }

        /// <summary>sim.Tick() 직후마다 호출. 이벤트는 매 틱, 프레임은 sampleEvery 틱마다.</summary>
        public void AfterTick(BattleSimulation sim)
        {
            _tick++;
            PollEvents(sim);
            if (_tick % _sampleEvery == 0)
            {
                CaptureFrame(sim);
            }
        }

        private void PollEvents(BattleSimulation sim)
        {
            for (int i = 0; i < _wasAlive.Length; i++)
            {
                bool alive = sim.IsAlive(i);
                if (_wasAlive[i] && !alive)
                {
                    _wasAlive[i] = false;
                    _data.events.Add(new ReplayEvent
                    {
                        type = "death", tick = _tick, unit = i,
                        squad = sim.GetSquadIndex(i), team = sim.GetTeam(i),
                    });
                }
            }

            for (int s = 0; s < _lastActivations.Length; s++)
            {
                int count = sim.GetSquadActivationCount(s);
                while (count > _lastActivations[s])
                {
                    _lastActivations[s]++;
                    _data.events.Add(new ReplayEvent
                    {
                        type = "activation", tick = _tick, unit = sim.GetGeneralUnit(s), squad = s,
                        team = SquadTeam(sim, s),
                    });
                }
                if (!_squadWiped[s] && sim.CountSquadSurvivors(s) == 0)
                {
                    _squadWiped[s] = true;
                    _data.events.Add(new ReplayEvent
                    {
                        type = "squadWiped", tick = _tick, unit = -1, squad = s, team = SquadTeam(sim, s),
                    });
                }
            }
        }

        private static int SquadTeam(BattleSimulation sim, int squadIndex)
        {
            for (int i = 0; i < sim.UnitCount; i++)
            {
                if (sim.GetSquadIndex(i) == squadIndex)
                {
                    return sim.GetTeam(i);
                }
            }
            return -1;
        }

        private void CaptureFrame(BattleSimulation sim)
        {
            int start = _bytes.Count;
            _bytes.Add(0); // 생존 수 자리 (뒤에서 채운다)
            _bytes.Add(0);

            int alive = 0;
            for (int i = 0; i < sim.UnitCount; i++)
            {
                if (!sim.IsAlive(i))
                {
                    continue;
                }
                System.Numerics.Vector2 p = sim.GetPosition(i);
                WriteU16(i);
                WriteI16(Quantize(p.X));
                WriteI16(Quantize(p.Y));
                _bytes.Add(HpByte(sim, i));
                alive++;
            }

            _bytes[start] = (byte)(alive & 0xFF);
            _bytes[start + 1] = (byte)((alive >> 8) & 0xFF);
            _data.frameCount++;
            _lastSampledTick = _tick;
        }

        /// <summary>
        /// 유닛 최대 체력은 시뮬이 공개하지 않는다 — 틱 0의 체력이 곧 최대치이므로 그것을 기준으로 쓴다
        /// (전투 시작 시 모든 유닛은 만피다). 뷰어에서는 점의 불투명도로만 쓰이므로 1/255 해상도면 충분하다.
        /// </summary>
        private readonly Dictionary<int, float> _maxHp = new Dictionary<int, float>();

        private byte HpByte(BattleSimulation sim, int unit)
        {
            float hp = sim.GetHp(unit);
            if (!_maxHp.TryGetValue(unit, out float max))
            {
                max = hp;
                _maxHp[unit] = max;
            }
            if (max <= 0f)
            {
                return 255;
            }
            int scaled = (int)Math.Round(hp / max * 255f);
            return (byte)Math.Clamp(scaled, 0, 255);
        }

        private static short Quantize(float value)
        {
            int q = (int)Math.Round(value * PositionScale);
            return (short)Math.Clamp(q, short.MinValue, short.MaxValue);
        }

        private void WriteU16(int value)
        {
            _bytes.Add((byte)(value & 0xFF));
            _bytes.Add((byte)((value >> 8) & 0xFF));
        }

        private void WriteI16(short value)
        {
            _bytes.Add((byte)(value & 0xFF));
            _bytes.Add((byte)((value >> 8) & 0xFF));
        }

        /// <summary>
        /// 마무리: 마지막 상태를 반드시 한 프레임 남기고, 통계 기록과의 자기 교차 검증을 건다.
        /// 마지막 프레임의 팀별 생존자가 SquadRecord 합계와 다르면 샘플링/양자화 어딘가가 깨진 것이다 —
        /// 조용히 틀린 리플레이를 내보내느니 여기서 실패한다.
        /// </summary>
        public ReplayData Finish(BattleSimulation sim, BattleRecord record)
        {
            if (_lastSampledTick != _tick)
            {
                CaptureFrame(sim);
            }
            _data.ticks = record.ticks;
            _data.winner = record.winner;
            _data.frames = Convert.ToBase64String(_bytes.ToArray());

            // 병사와 장군을 나눠 센다: CountSquadSurvivors는 계약상 장군을 제외하므로(아웃게임 survivals와
            // 같은 정의), 마지막 프레임의 전체 생존자와 직접 비교하면 살아남은 장군 수만큼 어긋난다.
            int soldiersLeft = 0, soldiersRight = 0, leadersLeft = 0, leadersRight = 0;
            for (int i = 0; i < sim.UnitCount; i++)
            {
                if (!sim.IsAlive(i))
                {
                    continue;
                }
                bool leader = sim.IsLeader(i);
                if (sim.GetTeam(i) == 0)
                {
                    if (leader) { leadersLeft++; } else { soldiersLeft++; }
                }
                else
                {
                    if (leader) { leadersRight++; } else { soldiersRight++; }
                }
            }

            Check("좌군 병사", soldiersLeft, SumSurvivors(record.leftSquads));
            Check("우군 병사", soldiersRight, SumSurvivors(record.rightSquads));
            Check("좌군 장군", leadersLeft, CountLivingGenerals(record.leftSquads));
            Check("우군 장군", leadersRight, CountLivingGenerals(record.rightSquads));
            return _data;
        }

        private void Check(string label, int fromReplay, int fromStats)
        {
            if (fromReplay != fromStats)
            {
                throw new InvalidDataException(
                    $"리플레이 자기 교차 검증 실패 (시드 {_data.seed}, {label}): " +
                    $"마지막 프레임 {fromReplay}명 vs 통계 기록 {fromStats}명 — 샘플링/양자화 경로를 의심하라");
            }
        }

        private static int CountLivingGenerals(List<SquadRecord> squads)
        {
            int count = 0;
            foreach (SquadRecord squad in squads)
            {
                if (squad.hasGeneral && squad.generalAlive)
                {
                    count++;
                }
            }
            return count;
        }

        private static int SumSurvivors(List<SquadRecord> squads)
        {
            int sum = 0;
            foreach (SquadRecord squad in squads)
            {
                sum += squad.survivors;
            }
            return sum;
        }

        /// <summary>덤프 전 예상 크기 — 큰 판이 모르는 새 수 MB로 뜨는 일이 없게 한다.</summary>
        public int ApproximateBytes => _bytes.Count * 4 / 3;
    }
}
