using NUnit.Framework;
using OutGame.Logic.Armies;

namespace OutGame.Tests.EditMode
{
    /// <summary>병과 → ArmyDefinition id 매핑 검증 (2026-08-02).</summary>
    public class ClassArmyDefinitionsTests
    {
        [TestCase(ArmyClass.Warrior, "army_warrior")]
        [TestCase(ArmyClass.Hunter, "army_hunter")]
        [TestCase(ArmyClass.Assassin, "army_assassin")]
        [TestCase(ArmyClass.Archer, "army_archer")]
        [TestCase(ArmyClass.None, "army_none")]
        public void DefIdFor_ReturnsExpectedId(ArmyClass armyClass, string expected)
        {
            Assert.AreEqual(expected, ClassArmyDefinitions.DefIdFor(armyClass));
        }
    }
}
