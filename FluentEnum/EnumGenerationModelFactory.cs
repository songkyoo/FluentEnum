using System.Text;
using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal static class EnumGenerationModelFactory
{
    public static EnumGenerationModel Create(
        INamedTypeSymbol enumSymbol,
        string accessModifier,
        EnumTargetKind targetKind
    )
    {
        var @namespace = enumSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : enumSymbol.ContainingNamespace.ToDisplayString();
        var typeNames = SymbolHelper
            .GetNestedTypeSymbols(enumSymbol)
            .Select(static typeSymbol => $"{typeSymbol.Name}{(typeSymbol.Arity > 0 ? $"_{typeSymbol.Arity}" : "")}");
        var qualifiedName = enumSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new EnumGenerationModel(
            ExtensionClass: new ExtensionClassModel(
                Namespace: @namespace,
                ClassName: $"{string.Join("_", typeNames)}Extensions",
                AccessModifier: accessModifier
            ),
            HintName: $"{enumSymbol.Name}_{enumSymbol.Arity}.{GetStableHash(qualifiedName):x8}.g.cs",
            ReceiverName: NamingHelper.GetEscapedKeyword(NamingHelper.GetCamelCaseName(enumSymbol.Name)),
            EnumType: EnumTypeModelFactory.Create(enumSymbol, targetKind)
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
