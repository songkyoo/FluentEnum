using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal static class EnumTypeModelFactory
{
    public static EnumTypeModel Create(
        INamedTypeSymbol enumSymbol,
        EnumTargetKind targetKind
    )
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

        if (!SymbolHelper.HasDuplicatedTypeParameterName(typeSymbols))
        {
            var typeParameters = typeSymbols
                .SelectMany(static symbol => symbol.TypeParameters)
                .ToArray();
            var genericParameters = string.Join(
                ", ",
                typeParameters.Select(static symbol => symbol.Name)
            );

            return new EnumTypeModel(
                Type: enumSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GenericParameters: genericParameters.Length > 0 ? $"<{genericParameters}>" : "",
                GenericParameterConstraints: typeParameters
                    .Select(static symbol => SymbolHelper.GetTypeParameterConstraintClause(
                        symbol,
                        static name => name
                    ))
                    .Where(static constraint => constraint.Length > 0)
                    .ToImmutableArray()
            );
        }

        var @namespace = enumSymbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? containingNamespace.ToDisplayString()
            : "";
        var types = new List<string>();
        var genericParameterConstraints = ImmutableArray.CreateBuilder<string>();
        var typeParameterIndex = 0;

        foreach (var symbol in typeSymbols)
        {
            var builder = new StringBuilder(symbol.Name);

            if (symbol.Arity > 0)
            {
                var mapper = new Dictionary<string, string>();

                builder.Append("<");

                for (var i = 0; i < symbol.Arity; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    var replacedTypeParameterName = $"T{typeParameterIndex + i}";

                    builder.Append(replacedTypeParameterName);
                    mapper.Add(symbol.TypeParameters[i].Name, replacedTypeParameterName);
                }

                builder.Append(">");

                typeParameterIndex += symbol.Arity;

                foreach (var typeParameterSymbol in symbol.TypeParameters)
                {
                    genericParameterConstraints.Add( SymbolHelper.GetTypeParameterConstraintClause(
                        typeParameterSymbol,
                        name => mapper[name]
                    ));
                }
            }

            types.Add(builder.ToString());
        }

        return new EnumTypeModel(
            Type: $"global::{(@namespace.Length > 0 ? $"{@namespace}." : "")}{string.Join(".", types)}",
            GenericParameters: typeParameterIndex > 0
                ? $"<{string.Join(", ", Enumerable.Range(0, typeParameterIndex).Select(static index => $"T{index}"))}>"
                : "",
            GenericParameterConstraints: genericParameterConstraints.ToImmutable()
        );
    }
}
