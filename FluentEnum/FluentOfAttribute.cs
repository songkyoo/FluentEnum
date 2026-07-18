using System.Diagnostics;

namespace Macaron.FluentEnum;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FluentOfAttribute(Type enumType) : Attribute
{
    public Type EnumType { get; } = enumType;

    public bool GenerateNegatedMembers { get; set; } = true;
}
