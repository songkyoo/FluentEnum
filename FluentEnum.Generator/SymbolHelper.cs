using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using System.Text;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.FluentEnum;

public static class SymbolHelper
{
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
        ImmutableDictionary<ITypeParameterSymbol, string> parameterMap
    )
    {
        var constraints = new List<string>();

        if (typeParameterSymbol.HasReferenceTypeConstraint)
        {
            constraints.Add(
                typeParameterSymbol.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class"
            );
        }
        else if (typeParameterSymbol.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (typeParameterSymbol.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        else if (typeParameterSymbol.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        foreach (var constraintType in typeParameterSymbol.ConstraintTypes)
        {
            constraints.Add(GetTypeString(constraintType, parameterMap));
        }

        if (typeParameterSymbol.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints.Count > 0
            ? $"where {parameterMap[typeParameterSymbol]} : {string.Join(", ", constraints)}"
            : "";
    }

    internal static string GetTypeString(
        ITypeSymbol typeSymbol,
        ImmutableDictionary<ITypeParameterSymbol, string> parameterMap
    )
    {
        var format = FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );
        var builder = new StringBuilder();

        foreach (var part in typeSymbol.ToDisplayParts(format))
        {
            builder.Append(part.Symbol is ITypeParameterSymbol parameter
                && parameterMap.TryGetValue(parameter, out var name)
                ? name
                : part.ToString()
            );
        }

        return builder.ToString();
    }
}
