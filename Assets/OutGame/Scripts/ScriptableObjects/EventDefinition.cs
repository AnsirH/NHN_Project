using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Events;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 이벤트 방 콘텐츠 에셋 (§5.4) — EventData(순수 로직)를 인스펙터에서 채운다.
    /// 일러스트는 SO 전용 필드 (순수 데이터엔 Unity 타입을 두지 않음).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Event Definition", fileName = "EventDefinition")]
    public class EventDefinition : ScriptableObject
    {
        [SerializeField] private string eventId;
        [SerializeField] private Sprite illustration;
        [TextArea(2, 5)] [SerializeField] private string bodyText;
        [SerializeField] private List<ChoiceEntry> choices = new List<ChoiceEntry>();

        public Sprite Illustration => illustration;

        public EventData ToData()
        {
            if (string.IsNullOrWhiteSpace(eventId))
                throw new InvalidOperationException($"{name}: eventId가 비어 있습니다.");
            if (choices.Count < 2 || choices.Count > 3)
                throw new InvalidOperationException($"{name}: 선택지는 2~3개여야 합니다 (§5.4). 현재: {choices.Count}");

            return new EventData
            {
                id = eventId,
                bodyText = bodyText,
                choices = choices.Select(c => c.ToData()).ToList(),
            };
        }

        [Serializable]
        public class ChoiceEntry
        {
            public string choiceText;
            public List<RewardEntry> rewards = new List<RewardEntry>();
            public string resultText;

            public EventChoiceData ToData()
            {
                List<RewardGrant> rewardData = rewards.Select(r => r.ToData()).ToList();
                // §4-7: 군대 보상이 여러 개면 상한 스킵 메시지가 몇 개나 실패했는지 구분 못 함 — 1개로 제한
                if (rewardData.Count(r => r.type == RewardType.Army) > 1)
                    throw new InvalidOperationException(
                        $"선택지 '{choiceText}': 군대 보상은 선택지당 1개까지만 허용됩니다 (§4-7).");

                return new EventChoiceData
                {
                    choiceText = choiceText,
                    rewards = rewardData,
                    resultText = resultText,
                };
            }
        }

        [Serializable]
        public class RewardEntry
        {
            public RewardType type;
            public ArmyDefinition armyDef;
            public ItemDefinition itemDef;
            [Min(0)] public int goldAmount;

            public RewardGrant ToData()
            {
                if (type == RewardType.Army && armyDef == null)
                    throw new InvalidOperationException("보상 type=Army인데 armyDef가 배정되지 않았습니다.");
                if (type == RewardType.Item && itemDef == null)
                    throw new InvalidOperationException("보상 type=Item인데 itemDef가 배정되지 않았습니다.");

                return new RewardGrant
                {
                    type = type,
                    armyDefId = armyDef != null ? armyDef.ToData().id : null,
                    itemId = itemDef != null ? itemDef.ToData().id : null,
                    goldAmount = goldAmount,
                };
            }
        }
    }
}
