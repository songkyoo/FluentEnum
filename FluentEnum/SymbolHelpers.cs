using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.FluentEnum;

public static class SymbolHelpers
{
    public static bool HasDisplayString(ISymbol? symbol, string displayString)
    {
        return symbol?.ToDisplayString() == displayString;
    }

    public static bool HasDuplicatedTypeParameterName(ImmutableArray<INamedTypeSymbol> typeSymbols)
    {
        var seen = new HashSet<string>();
        return typeSymbols.SelectMany(symbol => symbol.TypeParameters).Any(typeParam => !seen.Add(typeParam.Name));
    }

    public static ImmutableArray<INamedTypeSymbol> GetNestedTypeSymbols(INamedTypeSymbol typeSymbol)
    {
        var typeSymbols = new List<INamedTypeSymbol>();

        var parentTypeSymbol = typeSymbol;
        while (parentTypeSymbol != null)
        {
            typeSymbols.Add(parentTypeSymbol);
            parentTypeSymbol = parentTypeSymbol.ContainingType;
        }

        typeSymbols.Reverse();

        return typeSymbols.ToImmutableArray();
    }

    public static string GetTypeParameterConstraintClause(
        ITypeParameterSymbol typeParameterSymbol,
        Func<string, string> nameSelector
    )
    {
        var constraints = new List<string>();

        if (typeParameterSymbol.HasReferenceTypeConstraint)
        {
            constraints.Add("class");
        }

        if (typeParameterSymbol.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }

        if (typeParameterSymbol.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }

        foreach (var constraintType in typeParameterSymbol.ConstraintTypes)
        {
            constraints.Add(constraintType.ToDisplayString(FullyQualifiedFormat));
        }

        if (typeParameterSymbol.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        if (typeParameterSymbol.HasNotNullConstraint)
        {
            constraints.Add("not null");
        }

        return constraints.Count > 0
            ? $"where {nameSelector.Invoke(typeParameterSymbol.Name)} : {string.Join(", ", constraints)}"
            : "";
    }
}
