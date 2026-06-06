using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.Text;

using static System.Linq.Enumerable;
using static Macaron.FluentEnum.NamingHelpers;
using static Macaron.FluentEnum.SourceGenerationHelper;
using static Macaron.FluentEnum.SymbolHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.FluentEnum;

[Generator]
public class EnumExtensionsGenerator : IIncrementalGenerator
{
    #region Constants
    private const string FluentAttributeSource =
        """
        using System;

        namespace Macaron.FluentEnum
        {
            [AttributeUsage(AttributeTargets.Enum)]
            internal class FluentAttribute : Attribute
            {
                public FluentAttribute(bool generateNegatedMembers = true)
                {
                    GenerateNegatedMembers = generateNegatedMembers;
                }

                public bool GenerateNegatedMembers { get; }
            }

            [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
            internal class FluentOfAttribute : Attribute
            {
                public FluentOfAttribute(Type enumType, bool generateNegatedMembers = true)
                {
                    EnumType = enumType;
                    GenerateNegatedMembers = generateNegatedMembers;
                }

                public Type EnumType { get; }

                public bool GenerateNegatedMembers { get; }
            }
        }

        """;

    private const string FluentAttributeDisplayString = "Macaron.FluentEnum.FluentAttribute";
    private const string FluentOfAttributeDisplayString = "Macaron.FluentEnum.FluentOfAttribute";
    private const string FlagsAttributeDisplayString = "System.FlagsAttribute";

    private const string DefaultIndent = "    ";
    #endregion

    #region Constants - Diagnostics
    private static readonly DiagnosticDescriptor InvalidEnumAccessibilityRule = new(
        id: "MAFE0001",
        title: "Unsupported enum accessibility",
        messageFormat: "Enum '{0}' is declared with unsupported accessibility and cannot be processed by generator.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor InvalidFluentOfClassRule = new(
        id: "MAFE0002",
        title: "Unsupported FluentOf class",
        messageFormat: "Class '{0}' must be a non-generic, top-level static partial class to use FluentOf.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor InvalidFluentOfTargetRule = new(
        id: "MAFE0003",
        title: "Unsupported FluentOf target",
        messageFormat: "FluentOf on class '{0}' must reference an enum type.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor ConflictingClassMemberRule = new(
        id: "MAFE0004",
        title: "Class member conflicts with generated extension method",
        messageFormat: "Member '{0}' in class '{1}' conflicts with generated extension method '{2}'.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    #endregion


    #region Enums
    private enum EnumTargetKind
    {
        Definition,
        Closed,
    }
    #endregion

    #region Types
    private readonly record struct EnumMember(string Name, object Value);

    private readonly record struct GeneratedParameter(
        string Type,
        string Name,
        bool IsExtensionReceiver = false
    );

    private sealed record GeneratedMethod(
        string Name,
        string GenericParameters,
        int GenericArity,
        ImmutableArray<string> GenericParameterConstraints,
        ImmutableArray<GeneratedParameter> Parameters,
        string Body
    );

    private sealed record EnumContext(
        INamedTypeSymbol Symbol,
        string AccessModifier,
        ImmutableArray<EnumMember> Members,
        bool GenerateNegatedMembers,
        bool HasFlags,
        EnumTargetKind TargetKind
    );

    private sealed record FluentOfContext(
        INamedTypeSymbol ClassSymbol,
        EnumContext EnumContext
    );
    #endregion

    #region Static Methods
    private static (EnumContext?, ImmutableArray<Diagnostic>) GetAttributedEnumContext(
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
            if (HasDisplayString(attributeData.AttributeClass, FluentAttributeDisplayString))
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
                descriptor: InvalidEnumAccessibilityRule,
                location: diagnosticLocation,
                messageArgs: [symbol.Name]
            ));

            return ((EnumContext?)null, diagnostics.ToImmutable());
        }

        var hasFlags = definitionSymbol
            .GetAttributes()
            .Any(static attributeData => HasDisplayString(
                attributeData.AttributeClass,
                FlagsAttributeDisplayString
            ));
        var members = definitionSymbol
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Where(fieldSymbol => fieldSymbol.IsStatic && fieldSymbol.HasConstantValue)
            .Select(fieldSymbol => new EnumMember(
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

        #region Local Functions
        static string? GetAccessModifier(INamedTypeSymbol typeSymbol)
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
        #endregion
    }

    private static (FluentOfContext?, ImmutableArray<Diagnostic>) GetFluentOfContext(
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
                HasDisplayString(attributeData.AttributeClass, FluentOfAttributeDisplayString) &&
                IsAttributeOnDeclaration(attributeData, syntax)
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
                    descriptor: InvalidFluentOfClassRule,
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
                    descriptor: InvalidFluentOfTargetRule,
                    location: attributeLocation,
                    messageArgs: [classSymbol.Name]
                ))
            );
        }

        var (enumContext, diagnostics) = CreateEnumContext(
            symbol: enumSymbol,
            generateNegatedMembers: (bool)fluentOfAttribute.ConstructorArguments[1].Value!,
            diagnosticLocation: attributeLocation,
            targetKind: GetNestedTypeSymbols(enumSymbol).Any(static symbol => symbol.IsUnboundGenericType)
                ? EnumTargetKind.Definition
                : EnumTargetKind.Closed
        );

        return enumContext != null
            ? (new FluentOfContext(classSymbol, enumContext), diagnostics)
            : ((FluentOfContext?)null, diagnostics);

        #region Local Functions
        static INamedTypeSymbol? GetEnumTypeArgument(
            SemanticModel semanticModel,
            AttributeData attributeData
        )
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

        static bool IsAttributeOnDeclaration(
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

            return declaration.AttributeLists.Any(attributeList => attributeList.Span.Contains(attributeSyntax.Span));
        }
        #endregion
    }

    private static ImmutableArray<GeneratedMethod> GetGeneratedMethods(EnumContext enumContext)
    {
        var (symbol, _, members, generateNegatedMembers, hasFlags, targetKind) = enumContext;

        var (
            type,
            genericParameters,
            genericArity,
            genericParameterConstraints
        ) = GetStrings(symbol, targetKind);
        var escapedSymbolName = GetEscapedKeyword(GetCamelCaseName(symbol.Name));
        var memberSet = members.Select(x => x.Name).ToImmutableHashSet();

        var methods = ImmutableArray.CreateBuilder<GeneratedMethod>();
        var receiverParameter = new GeneratedParameter(
            Type: type,
            Name: escapedSymbolName,
            IsExtensionReceiver: true
        );

        // Is
        methods.Add(CreateMethod(
            name: "Is",
            parameters: ImmutableArray.Create(receiverParameter, new GeneratedParameter(type, "value")),
            body: $"return {escapedSymbolName} == value;"
        ));

        // IsNot
        methods.Add(CreateMethod(
            name: "IsNot",
            parameters: ImmutableArray.Create(receiverParameter, new GeneratedParameter(type, "value")),
            body: $"return {escapedSymbolName} != value;"
        ));

        // IsXXX
        foreach (var member in members)
        {
            methods.Add(CreateMethod(
                name: $"Is{member.Name}",
                parameters: ImmutableArray.Create(receiverParameter),
                body: $"return {escapedSymbolName} == {type}.{member.Name};"
            ));
        }

        // IsNotXXX
        if (generateNegatedMembers)
        {
            foreach (var member in members)
            {
                var negatedMember = GetNegatedMemberName(member.Name);

                if (memberSet.Contains(negatedMember))
                {
                    continue;
                }

                methods.Add(CreateMethod(
                    name: $"Is{negatedMember}",
                    parameters: ImmutableArray.Create(receiverParameter),
                    body: $"return {escapedSymbolName} != {type}.{member.Name};"
                ));
            }
        }

        if (hasFlags)
        {
            // Has
            methods.Add(CreateMethod(
                name: "Has",
                parameters: ImmutableArray.Create(receiverParameter, new GeneratedParameter(type, "value")),
                body: $"return ({escapedSymbolName} & value) == value;"
            ));

            // HasNot
            methods.Add(CreateMethod(
                name: "HasNot",
                parameters: ImmutableArray.Create(receiverParameter, new GeneratedParameter(type, "value")),
                body: $"return ({escapedSymbolName} & value) != value;"
            ));

            // HasXXX
            foreach (var member in members.Where(x => !IsZero(x.Value)))
            {
                methods.Add(CreateMethod(
                    name: $"Has{member.Name}",
                    parameters: ImmutableArray.Create(receiverParameter),
                    body: $"return ({escapedSymbolName} & {type}.{member.Name}) == {type}.{member.Name};"
                ));
            }

            // HasNotXXX
            if (generateNegatedMembers)
            {
                foreach (var member in members.Where(x => !IsZero(x.Value)))
                {
                    var negatedMember = GetNegatedMemberName(member.Name);

                    if (memberSet.Contains(negatedMember))
                    {
                        continue;
                    }

                    methods.Add(CreateMethod(
                        name: $"HasNot{member.Name}",
                        parameters: ImmutableArray.Create(receiverParameter),
                        body: $"return ({escapedSymbolName} & {type}.{member.Name}) != {type}.{member.Name};"
                    ));
                }
            }
        }

        return methods.ToImmutable();

        #region Local Functions
        GeneratedMethod CreateMethod(
            string name,
            ImmutableArray<GeneratedParameter> parameters,
            string body
        )
        {
            return new GeneratedMethod(
                Name: name,
                GenericParameters: genericParameters,
                GenericArity: genericArity,
                GenericParameterConstraints: genericParameterConstraints,
                Parameters: parameters,
                Body: body
            );
        }

        static string GetNegatedMemberName(string member) => member.StartsWith("Not") ? member[3..] : $"Not{member}";

        static bool IsZero(object value) => value switch
        {
            byte typedValue => typedValue == 0,
            sbyte typedValue => typedValue == 0,
            short typedValue => typedValue == 0,
            ushort typedValue => typedValue == 0,
            int typedValue => typedValue == 0,
            uint typedValue => typedValue == 0,
            long typedValue => typedValue == 0,
            ulong typedValue => typedValue == 0,
            _ => false,
        };
        #endregion
    }

    private static ImmutableArray<string> GenerateExtensionMethodCode(
        ImmutableArray<GeneratedMethod> methods,
        string indent
    )
    {
        var lines = ImmutableArray.CreateBuilder<string>();

        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];

            if (i > 0)
            {
                lines.Add("");
            }

            var parameters = string.Join(
                ", ",
                method.Parameters.Select(static parameter =>
                    $"{(parameter.IsExtensionReceiver ? "this " : "")}{parameter.Type} {parameter.Name}"
                )
            );

            lines.Add($"public static bool {method.Name}{method.GenericParameters}({parameters})");

            foreach (var constraint in method.GenericParameterConstraints)
            {
                lines.Add($"{indent}{constraint}");
            }

            lines.Add("{");
            lines.Add($"{indent}{method.Body}");
            lines.Add("}");
        }

        return lines.ToImmutable();
    }

    private static (
        ImmutableArray<GeneratedMethod> Methods,
        ImmutableArray<Diagnostic> Diagnostics
    ) FilterGeneratedMethods(
        INamedTypeSymbol classSymbol,
        EnumContext enumContext,
        ImmutableArray<GeneratedMethod> methods
    )
    {
        var filteredMethods = ImmutableArray.CreateBuilder<GeneratedMethod>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var method in methods)
        {
            var existingMembers = classSymbol.GetMembers(method.Name);
            var hasConflict = false;
            var hasEquivalentMethod = false;

            foreach (var existingMember in existingMembers)
            {
                if (existingMember is not IMethodSymbol existingMethod)
                {
                    AddConflictDiagnostic(existingMember, method);
                    hasConflict = true;

                    continue;
                }

                if (!HasSameCSharpSignature(existingMethod, method, enumContext))
                {
                    continue;
                }

                if (IsEquivalentExtensionMethod(existingMethod, method, enumContext))
                {
                    hasEquivalentMethod = true;
                }
                else
                {
                    AddConflictDiagnostic(existingMember, method);
                    hasConflict = true;
                }
            }

            if (!hasConflict && !hasEquivalentMethod)
            {
                filteredMethods.Add(method);
            }
        }

        return (filteredMethods.ToImmutable(), diagnostics.ToImmutable());

        #region Local Functions
        void AddConflictDiagnostic(ISymbol existingMember, GeneratedMethod generatedMethod)
        {
            diagnostics.Add(Diagnostic.Create(
                descriptor: ConflictingClassMemberRule,
                location: existingMember.Locations.FirstOrDefault(),
                messageArgs: [
                    existingMember.Name,
                    classSymbol.ToDisplayString(),
                    generatedMethod.Name,
                ]
            ));
        }

        static bool HasSameCSharpSignature(
            IMethodSymbol existingMethod,
            GeneratedMethod generatedMethod,
            EnumContext enumContext
        )
        {
            if (existingMethod.Arity != generatedMethod.GenericArity
                || existingMethod.Parameters.Length != generatedMethod.Parameters.Length
            )
            {
                return false;
            }

            for (var i = 0; i < existingMethod.Parameters.Length; i++)
            {
                var existingParameter = existingMethod.Parameters[i];

                if (existingParameter.RefKind != RefKind.None
                    || !IsTargetType(existingParameter.Type, existingMethod, enumContext
                ))
                {
                    return false;
                }
            }

            return true;
        }

        static bool IsTargetType(ITypeSymbol type, IMethodSymbol existingMethod, EnumContext enumContext)
        {
            if (type is not INamedTypeSymbol namedType)
            {
                return false;
            }

            if (enumContext.TargetKind == EnumTargetKind.Closed)
            {
                return SymbolEqualityComparer.Default.Equals(namedType, enumContext.Symbol);
            }

            var symbolComparer = SymbolEqualityComparer.Default;

            if (!symbolComparer.Equals(namedType.OriginalDefinition, enumContext.Symbol.OriginalDefinition))
            {
                return false;
            }

            var typeArguments = GetNestedTypeSymbols(namedType)
                .SelectMany(static symbol => symbol.TypeArguments)
                .ToImmutableArray();

            if (typeArguments.Length != existingMethod.TypeParameters.Length)
            {
                return false;
            }

            return !typeArguments
                .Where((symbol, i) => !symbolComparer.Equals(symbol, existingMethod.TypeParameters[i]))
                .Any();
        }

        static bool IsEquivalentExtensionMethod(
            IMethodSymbol method,
            GeneratedMethod generatedMethod,
            EnumContext enumContext
        )
        {
            if (method is not
            {
                MethodKind: MethodKind.Ordinary,
                IsStatic: true,
                IsExtensionMethod: true,
                DeclaredAccessibility: Accessibility.Public,
                ReturnType.SpecialType: SpecialType.System_Boolean,
                ReturnsByRef: false,
                ReturnsByRefReadonly: false,
            })
            {
                return false;
            }

            if (!method
                .Parameters
                .Zip(
                    generatedMethod.Parameters,
                    static (actual, _) => actual is
                    {
                        IsParams: false,
                        IsOptional: false,
                        HasExplicitDefaultValue: false,
                    }
                )
                .All(static isEquivalent => isEquivalent)
            )
            {
                return false;
            }

            return HasEquivalentTypeParameterConstraints(method, enumContext);
        }

        static bool HasEquivalentTypeParameterConstraints(IMethodSymbol method, EnumContext enumContext)
        {
            if (enumContext.TargetKind == EnumTargetKind.Closed)
            {
                return method.TypeParameters.IsEmpty;
            }

            var expectedTypeParameters = GetNestedTypeSymbols(enumContext.Symbol.OriginalDefinition)
                .SelectMany(static symbol => symbol.TypeParameters)
                .ToImmutableArray();

            if (expectedTypeParameters.Length != method.TypeParameters.Length)
            {
                return false;
            }

            for (var i = 0; i < expectedTypeParameters.Length; i++)
            {
                var expected = expectedTypeParameters[i];
                var actual = method.TypeParameters[i];

                if (expected.HasReferenceTypeConstraint != actual.HasReferenceTypeConstraint
                    || expected.ReferenceTypeConstraintNullableAnnotation != actual.ReferenceTypeConstraintNullableAnnotation
                    || expected.HasValueTypeConstraint != actual.HasValueTypeConstraint
                    || expected.HasUnmanagedTypeConstraint != actual.HasUnmanagedTypeConstraint
                    || expected.HasConstructorConstraint != actual.HasConstructorConstraint
                    || expected.HasNotNullConstraint != actual.HasNotNullConstraint
                    || expected.ConstraintTypes.Length != actual.ConstraintTypes.Length
                )
                {
                    return false;
                }

                for (var constraintIndex = 0; constraintIndex < expected.ConstraintTypes.Length; constraintIndex++)
                {
                    if (!AreEquivalentConstraintTypes(
                        expected.ConstraintTypes[constraintIndex],
                        actual.ConstraintTypes[constraintIndex],
                        expectedTypeParameters,
                        method.TypeParameters
                    ))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static bool AreEquivalentConstraintTypes(
            ITypeSymbol expected,
            ITypeSymbol actual,
            ImmutableArray<ITypeParameterSymbol> expectedTypeParameters,
            ImmutableArray<ITypeParameterSymbol> actualTypeParameters
        )
        {
            var symbolComparer = SymbolEqualityComparer.Default;

            switch (expected)
            {
                case ITypeParameterSymbol expectedTypeParameter:
                {
                    for (var i = 0; i < expectedTypeParameters.Length; i++)
                    {
                        if (symbolComparer.Equals(expectedTypeParameter, expectedTypeParameters[i]))
                        {
                            return
                                actual is ITypeParameterSymbol actualTypeParameter
                                && symbolComparer.Equals(actualTypeParameter, actualTypeParameters[i]);
                        }
                    }

                    return false;
                }
                case INamedTypeSymbol expectedNamedType when actual is INamedTypeSymbol actualNamedType:
                {
                    if (!symbolComparer.Equals(expectedNamedType.OriginalDefinition, actualNamedType.OriginalDefinition)
                        || expectedNamedType.TypeArguments.Length != actualNamedType.TypeArguments.Length
                    )
                    {
                        return false;
                    }

                    for (var i = 0; i < expectedNamedType.TypeArguments.Length; i++)
                    {
                        if (!AreEquivalentConstraintTypes(
                            expectedNamedType.TypeArguments[i],
                            actualNamedType.TypeArguments[i],
                            expectedTypeParameters,
                            actualTypeParameters
                        ))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                default:
                    return SymbolEqualityComparer.IncludeNullability.Equals(expected, actual);
            }
        }
        #endregion
    }

    private static (
        string Type,
        string GenericParameters,
        int GenericArity,
        ImmutableArray<string> GenericParameterConstraints
    ) GetStrings(INamedTypeSymbol typeSymbol, EnumTargetKind targetKind)
    {
        if (targetKind == EnumTargetKind.Closed)
        {
            return (
                Type: typeSymbol.ToDisplayString(FullyQualifiedFormat),
                GenericParameters: "",
                GenericArity: 0,
                GenericParameterConstraints: ImmutableArray<string>.Empty
            );
        }

        typeSymbol = typeSymbol.OriginalDefinition;

        var typeSymbols = GetNestedTypeSymbols(typeSymbol);

        if (!HasDuplicatedTypeParameterName(typeSymbols))
        {
            var typeParameters = typeSymbols
                .SelectMany(static symbol => symbol.TypeParameters)
                .ToArray();
            var genericParameters = string.Join(", ", typeParameters.Select(static symbol => symbol.Name));

            return (
                Type: typeSymbol.ToDisplayString(FullyQualifiedFormat),
                GenericParameters: genericParameters.Length > 0 ? $"<{genericParameters}>" : "",
                GenericArity: typeParameters.Length,
                GenericParameterConstraints: typeParameters
                    .Select(static symbol => GetTypeParameterConstraintClause(symbol, static name => name))
                    .Where(static constraint => constraint.Length > 0)
                    .ToImmutableArray()
            );
        }
        else
        {
            var @namespace = typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
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

                    for (int i = 0; i < symbol.Arity; i++)
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
                        genericParameterConstraints.Add(GetTypeParameterConstraintClause(
                            typeParameterSymbol,
                            name => mapper[name]
                        ));
                    }
                }

                types.Add(builder.ToString());
            }

            return (
                Type: $"global::{(@namespace.Length > 0 ? $"{@namespace}." : "")}{string.Join(".", types)}",
                GenericParameters: typeParameterIndex > 0
                    ? $"<{string.Join(", ", Range(0, typeParameterIndex).Select(static index => $"T{index}"))}>"
                    : "",
                GenericArity: typeParameterIndex,
                GenericParameterConstraints: genericParameterConstraints.ToImmutable()
            );
        }
    }
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddSource(
                hintName: "FluentAttribute.g.cs",
                sourceText: SourceText.From(text: FluentAttributeSource, encoding: Encoding.UTF8)
            );
        });

        var enumValuesProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is EnumDeclarationSyntax
                {
                    AttributeLists.Count: > 0,
                },
                transform: static (generatorSyntaxContext, _) => GetAttributedEnumContext(generatorSyntaxContext)
            );

        context.RegisterSourceOutput(enumValuesProvider, static (sourceProductionContext, context) =>
        {
            var (enumContext, diagnostics) = context;

            foreach (var diagnostic in diagnostics)
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }

            if (enumContext != null)
            {
                AddSource(
                    context: sourceProductionContext,
                    typeSymbol: enumContext.Symbol,
                    accessModifier: enumContext.AccessModifier,
                    lines: GenerateExtensionMethodCode(GetGeneratedMethods(enumContext), DefaultIndent),
                    indent: DefaultIndent
                );
            }
        });

        var fluentOfValuesProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax
                {
                    AttributeLists.Count: > 0,
                },
                transform: static (generatorSyntaxContext, _) => GetFluentOfContext(generatorSyntaxContext)
            );

        context.RegisterSourceOutput(fluentOfValuesProvider, static (sourceProductionContext, context) =>
        {
            var (fluentOfContext, diagnostics) = context;

            foreach (var diagnostic in diagnostics)
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }

            if (fluentOfContext != null)
            {
                var (classSymbol, enumContext) = fluentOfContext;
                var (methods, memberDiagnostics) = FilterGeneratedMethods(
                    classSymbol,
                    enumContext,
                    GetGeneratedMethods(enumContext)
                );

                foreach (var diagnostic in memberDiagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }

                AddSourceToPartialClass(
                    context: sourceProductionContext,
                    classSymbol: classSymbol,
                    targetTypeSymbol: enumContext.Symbol,
                    lines: GenerateExtensionMethodCode(methods, DefaultIndent),
                    indent: DefaultIndent
                );
            }
        });
    }
    #endregion
}
