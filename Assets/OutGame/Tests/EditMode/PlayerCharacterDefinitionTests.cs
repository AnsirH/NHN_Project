using System;
using System.Reflection;
using NUnit.Framework;
using OutGame.Logic.Characters;
using OutGame.ScriptableObjects;
using UnityEngine;

namespace OutGame.Tests.EditMode
{
    /// <summary>PlayerCharacterDefinition SO → PlayerCharacterData 변환 검증 (§5.2.5).</summary>
    public class PlayerCharacterDefinitionTests
    {
        [Test]
        public void ToData_ValidDefinition_ReturnsData()
        {
            var so = ScriptableObject.CreateInstance<PlayerCharacterDefinition>();
            try
            {
                SetField(so, "characterId", "char_1");
                SetField(so, "displayName", "캐릭터 1");
                SetField(so, "description", "설명");
                SetField(so, "skillId", "skill_char_1");
                SetField(so, "skillName", "스킬 1");
                SetField(so, "skillDescription", "스킬 설명");

                PlayerCharacterData data = so.ToData();

                Assert.AreEqual("char_1", data.id);
                Assert.AreEqual("캐릭터 1", data.displayName);
                Assert.AreEqual("설명", data.description);
                Assert.AreEqual("skill_char_1", data.skillId);
                Assert.AreEqual("스킬 1", data.skillName);
                Assert.AreEqual("스킬 설명", data.skillDescription);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToData_EmptyCharacterId_Throws()
        {
            var so = ScriptableObject.CreateInstance<PlayerCharacterDefinition>();
            try
            {
                SetField(so, "skillId", "skill_char_1");
                Assert.Throws<InvalidOperationException>(() => so.ToData());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToData_EmptySkillId_Throws()
        {
            var so = ScriptableObject.CreateInstance<PlayerCharacterDefinition>();
            try
            {
                SetField(so, "characterId", "char_1");
                Assert.Throws<InvalidOperationException>(() => so.ToData());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Accessors_ReflectSerializedFields()
        {
            var so = ScriptableObject.CreateInstance<PlayerCharacterDefinition>();
            try
            {
                SetField(so, "displayName", "캐릭터 1");
                SetField(so, "description", "설명");
                SetField(so, "skillName", "스킬 1");
                SetField(so, "skillDescription", "스킬 설명");
                SetField(so, "sortOrder", 2);

                Assert.AreEqual("캐릭터 1", so.DisplayName);
                Assert.AreEqual("설명", so.Description);
                Assert.AreEqual("스킬 1", so.SkillName);
                Assert.AreEqual("스킬 설명", so.SkillDescription);
                Assert.AreEqual(2, so.SortOrder);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        private static void SetField(PlayerCharacterDefinition target, string fieldName, object value)
        {
            FieldInfo field = typeof(PlayerCharacterDefinition).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }
    }
}
