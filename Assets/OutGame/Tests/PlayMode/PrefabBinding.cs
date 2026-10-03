using System.Reflection;
using NUnit.Framework;

namespace OutGame.Tests.PlayMode
{
    internal static class PrefabBinding
    {
        internal static T Get<T>(object owner, string fieldName) where T : class
        {
            var type = owner.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Missing binding {owner.GetType().Name}.{fieldName}");
            var value = field.GetValue(owner) as T;
            Assert.IsNotNull(value, $"Unwired binding {owner.GetType().Name}.{fieldName}");
            return value;
        }
    }
}
