using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal sealed record FluentOfContext(
    INamedTypeSymbol ClassSymbol,
    EnumContext EnumContext
);
