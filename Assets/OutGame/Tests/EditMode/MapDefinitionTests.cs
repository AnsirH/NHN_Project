using System;
using NUnit.Framework;
using OutGame.Logic.Maps;
using OutGame.ScriptableObjects;
using UnityEngine;

namespace OutGame.Tests.EditMode
{
    /// <summary>MapDefinition SO → MapGenerationConfig 변환 검증 (§5.2, §6).</summary>
    public class MapDefinitionTests
    {
        [Test]
        public void ToConfig_ValidDefinition_ReturnsValidatedConfig()
        {
            var so = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                SetField(so, "mapId", "map_default");
                SetField(so, "displayName", "북부 전선");

                MapGenerationConfig config = so.ToConfig();

                Assert.IsNotNull(config);
                Assert.DoesNotThrow(() => config.Validate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToConfig_EmptyMapId_Throws()
        {
            var so = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                SetField(so, "displayName", "북부 전선");
                Assert.Throws<InvalidOperationException>(() => so.ToConfig());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToConfig_EmptyDisplayName_Throws()
        {
            var so = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                SetField(so, "mapId", "map_default");
                Assert.Throws<InvalidOperationException>(() => so.ToConfig());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToConfig_InvalidGenerationConfig_Throws()
        {
            var so = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                SetField(so, "mapId", "map_default");
                SetField(so, "displayName", "북부 전선");
                SetField(so, "generationConfig", new MapGenerationConfig { floorCount = 1 });

                Assert.Throws<InvalidOperationException>(() => so.ToConfig());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ToConfig_ReturnsDefensiveCopy_MutationDoesNotAffectAsset()
        {
            var so = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                SetField(so, "mapId", "map_default");
                SetField(so, "displayName", "북부 전선");

                MapGenerationConfig first = so.ToConfig();
                first.floorCount = 999;

                MapGenerationConfig second = so.ToConfig();

                Assert.AreNotEqual(999, second.floorCount, "ToConfig()가 내부 필드의 원본 참조를 반환하면 안 됨");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Accessors_ReflectSerializedFields()
        {
            var so = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                SetField(so, "mapId", "map_default");
                SetField(so, "displayName", "북부 전선");
                SetField(so, "difficultyLabel", "보통");

                Assert.AreEqual("map_default", so.MapId);
                Assert.AreEqual("북부 전선", so.DisplayName);
                Assert.AreEqual("보통", so.DifficultyLabel);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        private static void SetField(MapDefinition target, string fieldName, object value)
        {
            var field = typeof(MapDefinition).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(target, value);
        }
    }
}
