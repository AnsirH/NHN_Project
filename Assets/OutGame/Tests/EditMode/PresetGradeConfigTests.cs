using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Logic.Battle;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 프리셋 등급별 요구 전투력 범위 검증 (2026-08-02) — authoring-time 참고용 데이터라
    /// EnemyCompositionGenerator는 안 쓰지만(EnemyPresetEditorWindow가 쓴다), 데이터 자체의
    /// 정합성은 다른 config들과 동일하게 검증한다.
    /// </summary>
    public class PresetGradeConfigTests
    {
        [Test]
        public void RequirementFor_DefinedGrade_ReturnsRequirement()
        {
            var config = new PresetGradeConfig
            {
                requirements = new List<PresetGradeRequirement>
                {
                    new PresetGradeRequirement { grade = 1, minPower = 30f, maxPower = 70f },
                },
            };

            PresetGradeRequirement result = config.RequirementFor(1);

            Assert.IsNotNull(result);
            Assert.AreEqual(30f, result.minPower, 1e-3f);
            Assert.AreEqual(70f, result.maxPower, 1e-3f);
        }

        [Test]
        public void RequirementFor_UndefinedGrade_ReturnsNull()
        {
            var config = new PresetGradeConfig();
            Assert.IsNull(config.RequirementFor(1));
        }

        [Test]
        public void Validate_EmptyRequirements_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new PresetGradeConfig().Validate());
        }

        [Test]
        public void Validate_DuplicateGrade_Throws()
        {
            var config = new PresetGradeConfig
            {
                requirements = new List<PresetGradeRequirement>
                {
                    new PresetGradeRequirement { grade = 1, minPower = 0f, maxPower = 10f },
                    new PresetGradeRequirement { grade = 1, minPower = 10f, maxPower = 20f },
                },
            };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_MaxBelowMin_Throws()
        {
            var config = new PresetGradeConfig
            {
                requirements = new List<PresetGradeRequirement> { new PresetGradeRequirement { grade = 1, minPower = 50f, maxPower = 10f } },
            };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Clone_ReturnsIndependentCopy()
        {
            var original = new PresetGradeConfig
            {
                requirements = new List<PresetGradeRequirement> { new PresetGradeRequirement { grade = 1, minPower = 0f, maxPower = 10f } },
            };
            PresetGradeConfig clone = original.Clone();
            clone.requirements[0].minPower = 999f;
            clone.requirements.Add(new PresetGradeRequirement { grade = 2, minPower = 0f, maxPower = 1f });

            Assert.AreNotEqual(999f, original.requirements[0].minPower);
            Assert.AreNotEqual(original.requirements.Count, clone.requirements.Count);
        }
    }
}
