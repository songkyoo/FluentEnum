using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal static class EnumTypeModelFactory
{
    public static EnumTypeModel Create(INamedTypeSymbol enumSymbol, EnumTargetKind targetKind)
    {
        if (targetKind == EnumTargetKind.Closed)
        {
            return new EnumTypeModel(
                Type: enumSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GenericParameters: "",
                GenericParameterConstraints: ImmutableArray<string>.Empty
            );
        }

        enumSymbol = enumSymbol.OriginalDefinition;

        var typeSymbols = SymbolHelper.GetNestedTypeSymbols(enumSymbol);
        var typeParameters = typeSymbols.SelectMany(static symbol => symbol.TypeParameters).ToArray();
        var renameParameters = SymbolHelper.HasDuplicatedTypeParameterName(typeSymbols);
        var map = ImmutableDictionary.CreateBuilder<ITypeParameterSymbol, string>(SymbolEqualityComparer.Default);

        for (var i = 0; i < typeParameters.Length; i++)
        {
            map.Add(typeParameters[i], renameParameters
                ? $"T{i}"
                : NamingHelper.GetEscapedKeyword(typeParameters[i].Name)
            );
        }

        var parameterMap = map.ToImmutable();
        var genericParameters = string.Join(", ", typeParameters.Select(parameter => parameterMap[parameter]));

        return new EnumTypeModel(
            Type: SymbolHelper.GetTypeString(enumSymbol, parameterMap),
            GenericParameters: genericParameters.Length > 0 ? $"<{genericParameters}>" : "",
            GenericParameterConstraints: typeParameters
                .Select(parameter => SymbolHelper.GetTypeParameterConstraintClause(parameter, parameterMap))
                .Where(static constraint => constraint.Length > 0)
                .ToImmutableArray()
        );
    }
}
