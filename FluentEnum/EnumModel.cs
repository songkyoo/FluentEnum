using System.Collections.Immutable;
namespace Macaron.FluentEnum;

public sealed record EnumModel(
    EnumGenerationModel Generation,
    ImmutableArray<EnumMember> Members,
    bool GenerateNegatedMembers,
    bool HasFlags
);
