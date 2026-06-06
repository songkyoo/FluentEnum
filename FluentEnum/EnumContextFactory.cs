using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.FluentEnum;

internal static class EnumContextFactory
{
    private const string FluentAttributeDisplayString = "Macaron.FluentEnum.FluentAttribute";
    private const string FluentOfAttributeDisplayString = "Macaron.FluentEnum.FluentOfAttribute";
    private const string FlagsAttributeDisplayString = "System.FlagsAttribute";

    public static (EnumContext?, ImmutableArray<Diagnostic>) GetAttributedEnumContext(
        GeneratorSyntaxContext generatorSyntaxContext
    )
    {
        if (generatorSyntaxContext.Node is not EnumDeclarationSyntax syntax)
        {
            return ((EnumContext?)null, ImmutableArray<Diagnostic>.Empty);
        }

        var semanticModel = generatorSyntaxContext.SemanticModel;
        var symbol = semanticModel.GetDeclaredSymbol(syntax);

        if (symbol == null)
        {
            return ((EnumContext?)null, ImmutableArray<Diagnostic>.Empty);
        }

        var fluentAttribute = (AttributeData?)null;

        foreach (var attributeData in symbol.GetAttributes())
        {
            if (SymbolHelpers.HasDisplayString(
                attributeData.AttributeClass,
                FluentAttributeDisplayString
            ))
            {
                fluentAttribute = attributeData;
            }
        }

        if (fluentAttribute == null)
        {
            return ((EnumContext?)null, ImmutableArray<Diagnostic>.Empty);
        }

        return CreateEnumContext(
            symbol: symbol,
            generateNegatedMembers: (bool)fluentAttribute.ConstructorArguments[0].Value!,
            diagnosticLocation: fluentAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
            targetKind: EnumTargetKind.Definition
        );
    }

    public static (FluentOfContext?, ImmutableArray<Diagnostic>) GetFluentOfContext(
        GeneratorSyntaxContext generatorSyntaxContext
    )
    {
        if (generatorSyntaxContext.Node is not ClassDeclarationSyntax syntax)
        {
            return ((FluentOfContext?)null, ImmutableArray<Diagnostic>.Empty);
        }

        var classSymbol = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(syntax);

        if (classSymbol == null)
        {
            return ((FluentOfContext?)null, ImmutableArray<Diagnostic>.Empty);
        }

        var fluentOfAttribute = classSymbol
            .GetAttributes()
            .FirstOrDefault(attributeData =>
                SymbolHelpers.HasDisplayString(attributeData.AttributeClass, FluentOfAttributeDisplayString)
                && IsAttributeOnDeclaration(attributeData, syntax)
            );

        if (fluentOfAttribute == null)
        {
            return ((FluentOfContext?)null, ImmutableArray<Diagnostic>.Empty);
        }

        var attributeLocation = fluentOfAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();

        if (!classSymbol.IsStatic
            || classSymbol.Arity > 0
            || classSymbol.ContainingType != null
            || classSymbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
            || !syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
        )
        {
            return (
                (FluentOfContext?)null,
                ImmutableArray.Create(Diagnostic.Create(
                    descriptor: GeneratorDiagnostics.InvalidFluentOfClass,
                    location: attributeLocation,
                    messageArgs: [classSymbol.Name]
                ))
            );
        }

        var enumSymbol = GetEnumTypeArgument(
            generatorSyntaxContext.SemanticModel,
            fluentOfAttribute
        );

        if (enumSymbol?.OriginalDefinition.TypeKind != TypeKind.Enum)
        {
            return (
                (FluentOfContext?)null,
                ImmutableArray.Create(Diagnostic.Create(
                    descriptor: GeneratorDiagnostics.InvalidFluentOfTarget,
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

        return enumContext != null
            ? (new FluentOfContext(classSymbol, enumContext), diagnostics)
            : ((FluentOfContext?)null, diagnostics);
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
                descriptor: GeneratorDiagnostics.InvalidEnumAccessibility,
                location: diagnosticLocation,
                messageArgs: [symbol.Name]
            ));

            return ((EnumContext?)null, diagnostics.ToImmutable());
        }

        var hasFlags = definitionSymbol
            .GetAttributes()
            .Any(static attributeData => SymbolHelpers.HasDisplayString(
                attributeData.AttributeClass,
                FlagsAttributeDisplayString
            ));
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
            return ((EnumContext?)null, diagnostics.ToImmutable());
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
        })
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

    private static bool IsAttributeOnDeclaration(
        AttributeData attributeData,
        ClassDeclarationSyntax declaration
    )
    {
        if (attributeData.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax attributeSyntax
            || attributeSyntax.SyntaxTree != declaration.SyntaxTree
        )
        {
            return false;
        }

        return declaration
            .AttributeLists
            .Any(attributeList => attributeList.Span.Contains(attributeSyntax.Span));
    }
}
