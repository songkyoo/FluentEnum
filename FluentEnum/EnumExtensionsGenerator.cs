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
        }

        """;

    private const string FluentAttributeDisplayString = "Macaron.FluentEnum.FluentAttribute";
    private const string FlagsAttributeDisplayString = "System.FlagsAttribute";

    private const string DefaultIndent = "    ";
    #endregion

    #region Types
    private readonly record struct EnumMember(string Name, object Value);

    private sealed record EnumContext(
        INamedTypeSymbol Symbol,
        string AccessModifier,
        ImmutableArray<EnumMember> Members,
        bool GenerateNegatedMembers,
        bool HasFlags
    );
    #endregion

    #region Static - Diagnostics
    private static readonly DiagnosticDescriptor InvalidEnumAccessibilityRule = new(
        id: "MAFE0001",
        title: "Unsupported enum accessibility",
        messageFormat: "Enum '{0}' is declared with unsupported accessibility and cannot be processed by generator.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    #endregion

    #region Static
    private static (EnumContext?, ImmutableArray<Diagnostic>) GetEnumContext(
        GeneratorSyntaxContext generatorSyntaxContext
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (generatorSyntaxContext.Node is not EnumDeclarationSyntax syntax)
        {
            return ((EnumContext?)null, diagnostics.ToImmutable());
        }

        var semanticModel = generatorSyntaxContext.SemanticModel;
        var symbol = semanticModel.GetDeclaredSymbol(syntax);

        if (symbol == null)
        {
            return ((EnumContext?)null, diagnostics.ToImmutable());
        }

        var fluentAttribute = (AttributeData?)null;
        var hasFlags = false;

        foreach (var attributeData in symbol.GetAttributes())
        {
            if (HasDisplayString(attributeData.AttributeClass, FluentAttributeDisplayString))
            {
                fluentAttribute = attributeData;
            }

            if (HasDisplayString(attributeData.AttributeClass, FlagsAttributeDisplayString))
            {
                hasFlags = true;
            }
        }

        if (fluentAttribute == null)
        {
            return ((EnumContext?)null, diagnostics.ToImmutable());
        }

        if (GetAccessModifier(symbol) is not { } accessModifier)
        {
            diagnostics.Add(Diagnostic.Create(
                descriptor: InvalidEnumAccessibilityRule,
                location: fluentAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                messageArgs: [symbol.Name]
            ));

            return ((EnumContext?)null, diagnostics.ToImmutable());
        }

        var members = symbol
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
                GenerateNegatedMembers: (bool)fluentAttribute.ConstructorArguments[0].Value!,
                HasFlags: hasFlags
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

    private static ImmutableArray<string> GenerateExtensionMethodCode(EnumContext enumContext, string indent)
    {
        var (symbol, _, members, generateNegatedMembers,hasFlags) = enumContext;

        var (
            type,
            genericParameters,
            genericParameterConstraints
        ) = GetStrings(symbol);
        var escapedSymbolName = GetEscapedKeyword(GetCamelCaseName(symbol.Name));
        var memberSet = members.Select(x => x.Name).ToImmutableHashSet();

        var lines = ImmutableArray.CreateBuilder<string>();

        // Is
        lines.Add($"public static bool Is{genericParameters}(this {type} {escapedSymbolName}, {type} value)");

        foreach (var constraint in genericParameterConstraints)
        {
            lines.Add($"{indent}{constraint}");
        }

        lines.Add($"{{");
        lines.Add($"{indent}return {escapedSymbolName} == value;");
        lines.Add($"}}");

        // IsNot
        lines.Add($"");
        lines.Add($"public static bool IsNot{genericParameters}(this {type} {escapedSymbolName}, {type} value)");

        foreach (var constraint in genericParameterConstraints)
        {
            lines.Add($"{indent}{constraint}");
        }

        lines.Add($"{{");
        lines.Add($"{indent}return {escapedSymbolName} != value;");
        lines.Add($"}}");

        // IsXXX
        foreach (var member in members)
        {
            lines.Add($"");
            lines.Add($"public static bool Is{member.Name}{genericParameters}(this {type} {escapedSymbolName})");

            foreach (var constraint in genericParameterConstraints)
            {
                lines.Add($"{indent}{constraint}");
            }

            lines.Add($"{{");
            lines.Add($"{indent}return {escapedSymbolName} == {type}.{member.Name};");
            lines.Add($"}}");
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

                lines.Add($"");
                lines.Add($"public static bool Is{negatedMember}{genericParameters}(this {type} {escapedSymbolName})");

                foreach (var constraint in genericParameterConstraints)
                {
                    lines.Add($"{indent}{constraint}");
                }

                lines.Add($"{{");
                lines.Add($"{indent}return {escapedSymbolName} != {type}.{member.Name};");
                lines.Add($"}}");
            }
        }

        if (hasFlags)
        {
            // Has
            lines.Add($"");
            lines.Add($"public static bool Has{genericParameters}(this {type} {escapedSymbolName}, {type} value)");

            foreach (var constraint in genericParameterConstraints)
            {
                lines.Add($"{indent}{constraint}");
            }

            lines.Add($"{{");
            lines.Add($"{indent}return ({escapedSymbolName} & value) == value;");
            lines.Add($"}}");

            // HasNot
            lines.Add($"");
            lines.Add($"public static bool HasNot{genericParameters}(this {type} {escapedSymbolName}, {type} value)");

            foreach (var constraint in genericParameterConstraints)
            {
                lines.Add($"{indent}{constraint}");
            }

            lines.Add($"{{");
            lines.Add($"{indent}return ({escapedSymbolName} & value) != value;");
            lines.Add($"}}");

            // HasXXX
            foreach (var member in members.Where(x => !IsZero(x.Value)))
            {
                lines.Add($"");
                lines.Add($"public static bool Has{member.Name}{genericParameters}(this {type} {escapedSymbolName})");

                foreach (var constraint in genericParameterConstraints)
                {
                    lines.Add($"{indent}{constraint}");
                }

                lines.Add($"{{");
                lines.Add($"{indent}return ({escapedSymbolName} & {type}.{member.Name}) == {type}.{member.Name};");
                lines.Add($"}}");
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

                    lines.Add($"");
                    lines.Add($"public static bool HasNot{member.Name}{genericParameters}(this {type} {escapedSymbolName})");

                    foreach (var constraint in genericParameterConstraints)
                    {
                        lines.Add($"{indent}{constraint}");
                    }

                    lines.Add($"{{");
                    lines.Add($"{indent}return ({escapedSymbolName} & {type}.{member.Name}) != {type}.{member.Name};");
                    lines.Add($"}}");
                }
            }
        }

        return lines.ToImmutable();

        #region Local Functions
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

    private static (
        string Type,
        string GenericParameters,
        ImmutableArray<string> GenericParameterConstraints
    ) GetStrings(INamedTypeSymbol typeSymbol)
    {
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

        IncrementalValuesProvider<(EnumContext?, ImmutableArray<Diagnostic>)> valuesProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is EnumDeclarationSyntax
                {
                    AttributeLists.Count: > 0,
                },
                transform: static (generatorSyntaxContext, _) => GetEnumContext(generatorSyntaxContext)
            );

        context.RegisterSourceOutput(valuesProvider, static (sourceProductionContext, context) =>
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
                    lines: GenerateExtensionMethodCode(enumContext, DefaultIndent),
                    indent: DefaultIndent
                );
            }
        });
    }
    #endregion
}
