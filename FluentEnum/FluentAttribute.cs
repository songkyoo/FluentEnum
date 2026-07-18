using System.Diagnostics;

namespace Macaron.FluentEnum;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public sealed class FluentAttribute(bool generateNegatedMembers = true) : Attribute
{
    public bool GenerateNegatedMembers { get; } = generateNegatedMembers;
}
