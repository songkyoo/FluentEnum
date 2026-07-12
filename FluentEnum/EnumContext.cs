using System.Collections.Immutable;
namespace Macaron.FluentEnum;

internal sealed record EnumContext(
    EnumTypeContext TypeContext,
    ImmutableArray<EnumMember> Members,
    bool GenerateNegatedMembers,
    bool HasFlags
);
