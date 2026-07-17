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
                || state.mapState.nodes.Count == 0 || state.armies == null || state.ownedItemIds == null)
                throw new ArgumentException("RunState JSON에 필수 데이터가 없습니다.", nameof(json));

            return state;
        }
    }
}
