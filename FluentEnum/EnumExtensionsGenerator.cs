using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Macaron.FluentEnum;

[Generator]
public class EnumExtensionsGenerator : IIncrementalGenerator
{
    private const string AttributeSources =
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

    private const string DefaultIndent = "    ";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddSource(
                hintName: "FluentAttribute.g.cs",
                sourceText: SourceText.From(text: AttributeSources, encoding: Encoding.UTF8)
            );
        });

        var enumValuesProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is EnumDeclarationSyntax
                {
                    AttributeLists.Count: > 0,
                },
                transform: static (generatorSyntaxContext, _) => EnumContextFactory.GetAttributedEnumContext(
                    generatorSyntaxContext
                )
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
                SourceGenerationHelper.AddSource(
                    context: sourceProductionContext,
                    typeSymbol: enumContext.Symbol,
                    accessModifier: enumContext.AccessModifier,
                    lines: ExtensionMethodRenderer.Render(
                        methods: ExtensionMethodFactory.Create(enumContext),
                        indent: DefaultIndent
                    ),
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
                transform: static (generatorSyntaxContext, _) => EnumContextFactory.GetFluentOfContext(
                    generatorSyntaxContext
                )
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
                var (methods, memberDiagnostics) = FluentOfMethodFilter.Filter(
                    classSymbol,
                    enumContext,
                    methods: ExtensionMethodFactory.Create(enumContext)
                );

                foreach (var diagnostic in memberDiagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }

                SourceGenerationHelper.AddSourceToPartialClass(
                    context: sourceProductionContext,
                    classSymbol: classSymbol,
                    targetTypeSymbol: enumContext.Symbol,
                    lines: ExtensionMethodRenderer.Render(methods, DefaultIndent),
                    indent: DefaultIndent
                );
            }
        });
    }
}
