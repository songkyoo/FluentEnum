using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.Accessibility;

namespace Macaron.FluentEnum;

internal static class AnalysisModelFactory
{
    private const string FlagsAttributeMetadataName = "System.FlagsAttribute";
    private const string FluentAttributeMetadataName = "Macaron.FluentEnum.FluentAttribute";

    public static AnalysisResult<EnumModel>? GetEnumModel(
        GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext
    )
    {
        if (generatorAttributeSyntaxContext.TargetSymbol is not INamedTypeSymbol enumSymbol)
        {
            return null;
        }

        if (generatorAttributeSyntaxContext.Attributes.Length != 1)
        {
            return null;
        }

        var fluentAttribute = generatorAttributeSyntaxContext.Attributes[0];

        return CreateEnumModel(
            enumSymbol: enumSymbol,
            generateNegatedMembers: (bool)fluentAttribute.ConstructorArguments[0].Value!,
            diagnosticLocation: fluentAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
            targetKind: EnumTargetKind.Definition
        );
    }

    public static AnalysisResult<FluentOfModel>? GetFluentOfModel(
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
            return null;
        }

        if (generatorAttributeSyntaxContext.Attributes.Length != 1)
        {
            return null;
        }

        var fluentOfAttribute = generatorAttributeSyntaxContext.Attributes[0];
        var attributeLocation = fluentOfAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();

        if (!classSymbol.IsStatic
            || classSymbol.Arity > 0
            || classSymbol.ContainingType != null
            || classSymbol.DeclaredAccessibility is not (Public or Internal)
            || !syntax.Modifiers.Any(SyntaxKind.PartialKeyword
        )
        )
        {
            return new AnalysisResult<FluentOfModel>.Failure(ImmutableArray.Create(Diagnostic.Create(
                descriptor: Diagnostics.InvalidFluentOfClass,
                location: attributeLocation,
                messageArgs: [classSymbol.Name]
            )));
        }

        var extensionClassModel = new ExtensionClassModel(
            Namespace: classSymbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : classSymbol.ContainingNamespace.ToDisplayString(),
            ClassName: NamingHelpers.GetEscapedKeyword(classSymbol.Name),
            AccessModifier: classSymbol.DeclaredAccessibility == Public
                ? "public"
                : "internal"
        );
        var enumSymbol = GetEnumTypeArgument(
            generatorAttributeSyntaxContext.SemanticModel,
            fluentOfAttribute
        );

        if (enumSymbol?.TypeKind == TypeKind.Error)
        {
            return null;
        }

        if (enumSymbol?.OriginalDefinition.TypeKind != TypeKind.Enum)
        {
            return new AnalysisResult<FluentOfModel>.Failure(ImmutableArray.Create(Diagnostic.Create(
                descriptor: Diagnostics.InvalidFluentOfTarget,
                location: attributeLocation,
                messageArgs: [classSymbol.Name]
            )));
        }

        if (enumSymbol.OriginalDefinition
            .GetAttributes()
            .Any(static attributeData => attributeData.AttributeClass?.ToDisplayString() == FluentAttributeMetadataName)
        )
        {
            return new AnalysisResult<FluentOfModel>.Failure(ImmutableArray.Create(Diagnostic.Create(
                descriptor: Diagnostics.FluentOfTargetHasFluent,
                location: attributeLocation,
                messageArgs: [classSymbol.Name, enumSymbol.Name]
            )));
        }

        var enumAnalysisResult = CreateEnumModel(
            enumSymbol: enumSymbol,
            generateNegatedMembers: (bool)fluentOfAttribute.ConstructorArguments[1].Value!,
            diagnosticLocation: attributeLocation,
            targetKind: SymbolHelpers
                .GetNestedTypeSymbols(enumSymbol)
                .Any(static symbol => symbol.IsUnboundGenericType)
                ? EnumTargetKind.Definition
                : EnumTargetKind.Closed
        );

        if (enumAnalysisResult == null)
        {
            return null;
        }

        switch (enumAnalysisResult)
        {
            case AnalysisResult<EnumModel>.Success success:
            {
                return new AnalysisResult<FluentOfModel>.Success(new FluentOfModel(
                    ExtensionClass: extensionClassModel,
                    Enum: success.Model
                ));
            }
            case AnalysisResult<EnumModel>.Failure failure:
            {
                return new AnalysisResult<FluentOfModel>.Failure(failure.Diagnostics);
            }
            default:
                throw new InvalidOperationException($"Invalid analysis result: {enumAnalysisResult}");
        }
    }

    private static AnalysisResult<EnumModel>? CreateEnumModel(
        INamedTypeSymbol enumSymbol,
        bool generateNegatedMembers,
        Location? diagnosticLocation,
        EnumTargetKind targetKind
    )
    {
        var definitionSymbol = enumSymbol.OriginalDefinition;

        if (GetAccessModifier(definitionSymbol) is not { } accessModifier)
        {
            return new AnalysisResult<EnumModel>.Failure(ImmutableArray.Create(Diagnostic.Create(
                descriptor: Diagnostics.InvalidEnumAccessibility,
                location: diagnosticLocation,
                messageArgs: [enumSymbol.Name]
            )));
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
            return null;
        }

        return new AnalysisResult<EnumModel>.Success(new EnumModel(
            Generation: EnumGenerationModelFactory.Create(enumSymbol, accessModifier, targetKind),
            Members: members,
            GenerateNegatedMembers: generateNegatedMembers,
            HasFlags: hasFlags
        ));
    }

    private static string? GetAccessModifier(INamedTypeSymbol typeSymbol)
    {
        var result = "public";
        var parentTypeSymbol = typeSymbol;

        while (parentTypeSymbol != null)
        {
            switch (parentTypeSymbol.DeclaredAccessibility)
            {
                case Public:
                    break;
                case Internal:
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
