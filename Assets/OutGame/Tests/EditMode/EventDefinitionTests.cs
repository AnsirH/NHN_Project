using NUnit.Framework;
using OutGame.Logic.Events;
using OutGame.ScriptableObjects;
using UnityEngine;

namespace OutGame.Tests.EditMode
{
    /// <summary>EventDefinition SO → EventData 변환 검증 (§5.4).</summary>
    public class EventDefinitionTests
    {
        [Test]
        public void ToData_MapsIdBodyAndChoices()
        {
            var so = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                SetField(so, "eventId", "evt_test");
                SetField(so, "bodyText", "테스트 본문");
                SetField(so, "choices", new System.Collections.Generic.List<EventDefinition.ChoiceEntry>
                {
                    new EventDefinition.ChoiceEntry
                    {
                        choiceText = "선택지 A",
                        resultText = "결과 A",
                        rewards = new System.Collections.Generic.List<EventDefinition.RewardEntry>
                        {
                            new EventDefinition.RewardEntry { type = RewardType.Gold, goldAmount = 20 },
                        },
                    },
                    new EventDefinition.ChoiceEntry { choiceText = "선택지 B", resultText = "결과 B" },
                });

                EventData data = so.ToData();

                Assert.AreEqual("evt_test", data.id);
                Assert.AreEqual("테스트 본문", data.bodyText);
                Assert.AreEqual(2, data.choices.Count);
                Assert.AreEqual("선택지 A", data.choices[0].choiceText);
                Assert.AreEqual(RewardType.Gold, data.choices[0].rewards[0].type);
                Assert.AreEqual(20, data.choices[0].rewards[0].goldAmount);
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToData_FewerThanTwoChoices_Throws()
        {
            var so = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                SetField(so, "eventId", "evt_test");
                SetField(so, "choices", new System.Collections.Generic.List<EventDefinition.ChoiceEntry>
                {
                    new EventDefinition.ChoiceEntry { choiceText = "선택지 A" },
                });

                Assert.Throws<System.InvalidOperationException>(() => so.ToData());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToData_EmptyEventId_Throws()
        {
            var so = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => so.ToData());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToData_MoreThanThreeChoices_Throws()
        {
            var so = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                SetField(so, "eventId", "evt_test");
                SetField(so, "choices", new System.Collections.Generic.List<EventDefinition.ChoiceEntry>
                {
                    new EventDefinition.ChoiceEntry { choiceText = "A" },
                    new EventDefinition.ChoiceEntry { choiceText = "B" },
                    new EventDefinition.ChoiceEntry { choiceText = "C" },
                    new EventDefinition.ChoiceEntry { choiceText = "D" },
                });

                Assert.Throws<System.InvalidOperationException>(() => so.ToData());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ChoiceEntry_MultipleArmyRewards_Throws()
        {
            var choice = new EventDefinition.ChoiceEntry
            {
                choiceText = "선택지",
                rewards = new System.Collections.Generic.List<EventDefinition.RewardEntry>
                {
                    new EventDefinition.RewardEntry { type = RewardType.Army, armyClass = OutGame.Logic.Armies.ArmyClass.Warrior },
                    new EventDefinition.RewardEntry { type = RewardType.Army, armyClass = OutGame.Logic.Armies.ArmyClass.Archer },
                },
            };

            Assert.Throws<System.InvalidOperationException>(() => choice.ToData());
        }

        [Test]
        public void ChoiceEntry_MultipleNonArmyRewards_DoesNotThrow()
        {
            var choice = new EventDefinition.ChoiceEntry
            {
                choiceText = "선택지",
                rewards = new System.Collections.Generic.List<EventDefinition.RewardEntry>
                {
                    new EventDefinition.RewardEntry { type = RewardType.Gold, goldAmount = 1 },
                    new EventDefinition.RewardEntry { type = RewardType.Gold, goldAmount = 2 },
                },
            };

            // Gold 2개는 허용 — Army 2개일 때만 제한됨을 대조 확인
            Assert.DoesNotThrow(() => choice.ToData());
        }

        [Test]
        public void RewardEntry_ItemTypeWithoutItemDef_Throws()
        {
            var entry = new EventDefinition.RewardEntry { type = RewardType.Item, itemDef = null };
            Assert.Throws<System.InvalidOperationException>(() => entry.ToData());
        }

        [Test]
        public void RewardEntry_GoldType_DoesNotRequireDefs()
        {
            var entry = new EventDefinition.RewardEntry { type = RewardType.Gold, goldAmount = 10 };
            Assert.DoesNotThrow(() => entry.ToData());
        }

        private static void SetField(EventDefinition target, string fieldName, object value)
        {
            var field = typeof(EventDefinition).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(target, value);
        }
    }
}
