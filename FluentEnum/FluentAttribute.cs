using System.Diagnostics;

namespace Macaron.FluentEnum;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(AttributeTargets.Enum)]
public sealed class FluentAttribute : Attribute
{
    public bool GenerateNegatedMembers { get; set; } = true;
}
