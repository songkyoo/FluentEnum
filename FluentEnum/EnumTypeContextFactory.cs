using System.Text;
using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal static class EnumTypeContextFactory
{
    public static EnumTypeContext Create(
        INamedTypeSymbol symbol,
        string accessModifier,
        EnumTargetKind targetKind
    )
    {
        var @namespace = symbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : symbol.ContainingNamespace.ToDisplayString();
        var typeNames = SymbolHelpers
            .GetNestedTypeSymbols(symbol)
            .Select(static typeSymbol =>
                $"{typeSymbol.Name}{(typeSymbol.Arity > 0 ? $"_{typeSymbol.Arity}" : "")}"
            );
        var qualifiedName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new EnumTypeContext(
            Namespace: @namespace,
            ExtensionClassName: $"{string.Join("_", typeNames)}Extensions",
            AccessModifier: accessModifier,
            HintName: $"{symbol.Name}_{symbol.Arity}.{GetStableHash(qualifiedName):x8}.g.cs",
            ReceiverName: NamingHelpers.GetEscapedKeyword(NamingHelpers.GetCamelCaseName(symbol.Name)),
            GeneratedType: GeneratedEnumTypeFactory.Create(symbol, targetKind)
        );
    }

    private static uint GetStableHash(string value)
    {
        const uint fnvPrime = 16777619;
        const uint offsetBasis = 2166136261;

        var bytes = Encoding.UTF8.GetBytes(value);
        uint hash = offsetBasis;

        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash;
    }
}
