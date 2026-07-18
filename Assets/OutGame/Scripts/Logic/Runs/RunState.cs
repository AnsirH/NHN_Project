using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Maps;
using UnityEngine;

namespace OutGame.Logic.Runs
{
    /// <summary>
    /// 런 전체 상태 — 저장 대상 (상세 기획 §6). JSON 직렬화로 이어하기를 지원한다.
    /// </summary>
    [Serializable]
    public class RunState
    {
        public MapState mapState;
        public List<ArmyInstance> armies = new List<ArmyInstance>();
        public List<string> ownedItemIds = new List<string>();
        public int gold; // §4-20: 획득만 1차 구현
        public List<string> visitedEventIds = new List<string>(); // 동일 런 내 이벤트 중복 방지 (§5.4)

        // 배치 슬롯 진형 — 방을 넘어가도 유지되어야 하므로 패널 로컬이 아니라 여기 저장 (§5.7 2026-07-19 개정).
        public List<ArmySlotAssignment> deployment = new List<ArmySlotAssignment>();

        public ArmyInstance GetArmy(string instanceId) =>
            armies.FirstOrDefault(a => a.instanceId == instanceId);

        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true);

        public static RunState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("RunState JSON이 비어 있습니다.", nameof(json));

            RunState state;
            try
            {
                state = JsonUtility.FromJson<RunState>(json);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"RunState JSON 파싱 실패: {e.Message}", nameof(json), e);
            }

            if (state == null || state.mapState == null || state.mapState.nodes == null
                || state.mapState.nodes.Count == 0 || state.armies == null || state.ownedItemIds == null
                || state.visitedEventIds == null || state.deployment == null)
                throw new ArgumentException("RunState JSON에 필수 데이터가 없습니다.", nameof(json));

            return state;
        }
    }
}
