using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal sealed record EnumContext(
    INamedTypeSymbol Symbol,
    string AccessModifier,
    ImmutableArray<EnumMember> Members,
    bool GenerateNegatedMembers,
    bool HasFlags,
    EnumTargetKind TargetKind
);
