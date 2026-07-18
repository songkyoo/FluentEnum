using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Macaron.FluentEnum.SymbolHelper;
using static Microsoft.CodeAnalysis.Accessibility;

namespace Macaron.FluentEnum;

internal static class AnalysisModelFactory
{
    private const string FlagsAttributeMetadataName = "System.FlagsAttribute";
    private const string FluentAttributeMetadataName = "Macaron.FluentEnum.FluentAttribute";
    private const string GenerateNegatedMembersPropertyName = "GenerateNegatedMembers";

    public static AnalysisResult<EnumModel>? GetEnumModel(
        GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext,
        CancellationToken cancellationToken
    )
    {
        if (generatorAttributeSyntaxContext.Attributes.Length != 1)
        {
            return null;
        }

        if (generatorAttributeSyntaxContext.TargetSymbol is not INamedTypeSymbol enumSymbol)
        {
            return null;
        }

        var fluentAttribute = generatorAttributeSyntaxContext.Attributes[0];

        return CreateEnumModel(
            enumSymbol: enumSymbol,
            generateNegatedMembers: GetGenerateNegatedMembers(fluentAttribute),
            diagnosticLocation: fluentAttribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation(),
            targetKind: EnumTargetKind.Definition,
            cancellationToken: cancellationToken
        );
    }

    public static AnalysisResult<FluentOfModel>? GetFluentOfModel(
        GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext,
        CancellationToken cancellationToken
    )
    {
        if (generatorAttributeSyntaxContext.Attributes.Length != 1)
        {
            return null;
        }

        if (generatorAttributeSyntaxContext is not
            {
                TargetNode: ClassDeclarationSyntax syntax,
                TargetSymbol: INamedTypeSymbol classSymbol,
            }
        )
        {
            return null;
        }

        var fluentOfAttribute = generatorAttributeSyntaxContext.Attributes[0];
        var attributeLocation = fluentOfAttribute
            .ApplicationSyntaxReference?
            .GetSyntax(cancellationToken)
            .GetLocation();

        if (!classSymbol.IsStatic
            || classSymbol.Arity > 0
            || classSymbol.ContainingType != null
            || classSymbol.DeclaredAccessibility is not (Public or Internal)
            || !syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
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
            ClassName: NamingHelper.GetEscapedKeyword(classSymbol.Name),
            AccessModifier: classSymbol.DeclaredAccessibility == Public
                ? "public"
                : "internal"
        );
        var enumSymbol = GetEnumTypeArgument(
            generatorAttributeSyntaxContext.SemanticModel,
            fluentOfAttribute,
            cancellationToken
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

        if (enumSymbol
            .OriginalDefinition
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
            generateNegatedMembers: GetGenerateNegatedMembers(fluentOfAttribute),
            diagnosticLocation: attributeLocation,
            targetKind: GetNestedTypeSymbols(enumSymbol).Any(static symbol => symbol.IsUnboundGenericType)
                ? EnumTargetKind.Definition
                : EnumTargetKind.Closed,
            cancellationToken
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
        EnumTargetKind targetKind,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        var members = ImmutableArray.CreateBuilder<EnumMember>();

        foreach (var memberSymbol in definitionSymbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (memberSymbol is not IFieldSymbol { IsStatic: true, HasConstantValue: true } fieldSymbol)
            {
                continue;
            }

            members.Add(new EnumMember(
                Name: fieldSymbol.Name,
                Value: fieldSymbol.ConstantValue!
            ));
        }

        if (members.Count < 1)
        {
            return null;
        }

        return new AnalysisResult<EnumModel>.Success(new EnumModel(
            Generation: EnumGenerationModelFactory.Create(enumSymbol, accessModifier, targetKind),
            Members: members.ToImmutable(),
            GenerateNegatedMembers: generateNegatedMembers,
            HasFlags: hasFlags
        ));
    }

    private static bool GetGenerateNegatedMembers(AttributeData attributeData)
    {
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument is
                {
                    Key: GenerateNegatedMembersPropertyName,
                    Value.Value: bool generateNegatedMembers,
                }
            )
            {
                return generateNegatedMembers;
            }
        }

        return true;
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

    private static INamedTypeSymbol? GetEnumTypeArgument(
        SemanticModel semanticModel,
        AttributeData attributeData,
        CancellationToken cancellationToken
    )
    {
        if (attributeData.ConstructorArguments[0].Value is INamedTypeSymbol typeSymbol)
        {
            return typeSymbol;
        }

        if (attributeData.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is AttributeSyntax
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
                return semanticModel.GetTypeInfo(typeOfExpression.Type, cancellationToken).Type as INamedTypeSymbol;
            }
        }

        return null;
    }
}
