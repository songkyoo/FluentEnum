using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.FluentEnum;

internal static class EnumContextFactory
{
    internal const string FluentAttributeMetadataName = "Macaron.FluentEnum.FluentAttribute";
    internal const string FluentOfAttributeMetadataName = "Macaron.FluentEnum.FluentOfAttribute";
    private const string FlagsAttributeMetadataName = "System.FlagsAttribute";

    public static (EnumContext?, ImmutableArray<Diagnostic>) GetFluentContext(
        GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext
    )
    {
        if (generatorAttributeSyntaxContext.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return (null, ImmutableArray<Diagnostic>.Empty);
        }

        var fluentAttribute = generatorAttributeSyntaxContext.Attributes[0];

        return CreateEnumContext(
            symbol: symbol,
            generateNegatedMembers: (bool)fluentAttribute.ConstructorArguments[0].Value!,
            diagnosticLocation: fluentAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
            targetKind: EnumTargetKind.Definition
        );
    }

    public static AnalysisResult<FluentOfContext> GetFluentOfContext(
        GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext
    )
    {
        if (generatorAttributeSyntaxContext is not
            {
                TargetNode: ClassDeclarationSyntax syntax,
                TargetSymbol: INamedTypeSymbol classSymbol,
            }
        )
        {
            return new AnalysisResult<FluentOfContext>.Failure(ImmutableArray<Diagnostic>.Empty);
        }

        if (generatorAttributeSyntaxContext.Attributes.Length != 1)
        {
            return new AnalysisResult<FluentOfContext>.Failure(ImmutableArray<Diagnostic>.Empty);
        }

        var fluentOfAttribute = generatorAttributeSyntaxContext.Attributes[0];

        var attributeLocation = fluentOfAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();

        if (!classSymbol.IsStatic
            || classSymbol.Arity > 0
            || classSymbol.ContainingType != null
            || classSymbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
            || !syntax.Modifiers.Any(SyntaxKind.PartialKeyword
        )
        )
        {
            return new AnalysisResult<FluentOfContext>.Failure(
                ImmutableArray.Create(Diagnostic.Create(
                    descriptor: Diagnostics.InvalidFluentOfClass,
                    location: attributeLocation,
                    messageArgs: [classSymbol.Name]
                ))
            );
        }

        var enumSymbol = GetEnumTypeArgument(
            generatorAttributeSyntaxContext.SemanticModel,
            fluentOfAttribute
        );

        if (enumSymbol?.TypeKind == TypeKind.Error)
        {
            return new AnalysisResult<FluentOfContext>.Failure(ImmutableArray<Diagnostic>.Empty);
        }

        if (enumSymbol?.OriginalDefinition.TypeKind != TypeKind.Enum)
        {
            return new AnalysisResult<FluentOfContext>.Failure(
                ImmutableArray.Create(Diagnostic.Create(
                    descriptor: Diagnostics.InvalidFluentOfTarget,
                    location: attributeLocation,
                    messageArgs: [classSymbol.Name]
                ))
            );
        }

        var (enumContext, diagnostics) = CreateEnumContext(
            symbol: enumSymbol,
            generateNegatedMembers: (bool)fluentOfAttribute.ConstructorArguments[1].Value!,
            diagnosticLocation: attributeLocation,
            targetKind: SymbolHelpers
                .GetNestedTypeSymbols(enumSymbol)
                .Any(static symbol => symbol.IsUnboundGenericType)
                ? EnumTargetKind.Definition
                : EnumTargetKind.Closed
        );

        if (enumContext != null)
        {
            return new AnalysisResult<FluentOfContext>.Success(
                ImmutableArray.Create(new FluentOfContext(classSymbol, enumContext))
            );
        }

        return diagnostics.IsEmpty
            ? new AnalysisResult<FluentOfContext>.Success(ImmutableArray<FluentOfContext>.Empty)
            : new AnalysisResult<FluentOfContext>.Failure(diagnostics);
    }

    private static (EnumContext?, ImmutableArray<Diagnostic>) CreateEnumContext(
        INamedTypeSymbol symbol,
        bool generateNegatedMembers,
        Location? diagnosticLocation,
        EnumTargetKind targetKind
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var definitionSymbol = symbol.OriginalDefinition;

        if (GetAccessModifier(definitionSymbol) is not { } accessModifier)
        {
            diagnostics.Add(Diagnostic.Create(
                descriptor: Diagnostics.InvalidEnumAccessibility,
                location: diagnosticLocation,
                messageArgs: [symbol.Name]
            ));

            return (null, diagnostics.ToImmutable());
        }

        var hasFlags = definitionSymbol
            .GetAttributes()
            .Any(static attributeData => attributeData.AttributeClass?.ToDisplayString() == FlagsAttributeMetadataName);
        var members = definitionSymbol
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static fieldSymbol => fieldSymbol.IsStatic && fieldSymbol.HasConstantValue)
            .Select(static fieldSymbol => new EnumMember(
                Name: fieldSymbol.Name,
                Value: fieldSymbol.ConstantValue!
            ))
            .ToImmutableArray();

        if (members.Length < 1)
        {
            return (null, diagnostics.ToImmutable());
        }

        return (
            new EnumContext(
                Symbol: symbol,
                AccessModifier: accessModifier,
                Members: members,
                GenerateNegatedMembers: generateNegatedMembers,
                HasFlags: hasFlags,
                TargetKind: targetKind
            ),
            diagnostics.ToImmutable()
        );
    }

    private static string? GetAccessModifier(INamedTypeSymbol typeSymbol)
    {
        var result = "public";
        var parentTypeSymbol = typeSymbol;

        while (parentTypeSymbol != null)
        {
            switch (parentTypeSymbol.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    break;
                case Accessibility.Internal:
                    result = "internal";
                    break;
                default:
                    return null;
            }

            parentTypeSymbol = parentTypeSymbol.ContainingType;
        }

        return result;
    }

    private static INamedTypeSymbol? GetEnumTypeArgument(SemanticModel semanticModel, AttributeData attributeData)
    {
        if (attributeData.ConstructorArguments[0].Value is INamedTypeSymbol typeSymbol)
        {
            return typeSymbol;
        }

        if (attributeData.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax
            {
                ArgumentList.Arguments: var arguments,
            }
        )
        {
            var typeOfExpression = arguments
                .Select(static argument => argument.Expression)
                .OfType<TypeOfExpressionSyntax>()
                .FirstOrDefault();

            if (typeOfExpression != null)
            {
                return semanticModel.GetTypeInfo(typeOfExpression.Type).Type as INamedTypeSymbol;
            }
        }

        return null;
    }
}
