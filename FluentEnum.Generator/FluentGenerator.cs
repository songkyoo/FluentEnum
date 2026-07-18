using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Macaron.FluentEnum;

[Generator]
public class FluentGenerator : IIncrementalGenerator
{
    #region Constants
    private const string FluentAttributeMetadataName = "Macaron.FluentEnum.FluentAttribute";
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analysisResult = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: FluentAttributeMetadataName,
                predicate: static (syntaxNode, _) => syntaxNode is EnumDeclarationSyntax,
                transform: static (generatorAttributeSyntaxContext, cancellationToken) => AnalysisModelFactory.GetEnumModel(
                    generatorAttributeSyntaxContext,
                    cancellationToken
                )
            )
            .Where(static result => result is not null)
            .Select(static (result, _) => result!);
        var diagnosticProvider = analysisResult
            .Where(static result => result is AnalysisResult<EnumModel>.Failure)
            .SelectMany(static (result, _) => ((AnalysisResult<EnumModel>.Failure)result).Diagnostics);
        var enumModelProvider = analysisResult
            .Where(static result => result is AnalysisResult<EnumModel>.Success)
            .Select(static (result, _) => ((AnalysisResult<EnumModel>.Success)result).Model)
            .WithComparer(EnumModelComparer.Instance);

        context.RegisterSourceOutput(diagnosticProvider, static (sourceProductionContext, diagnostic) =>
        {
            sourceProductionContext.ReportDiagnostic(diagnostic);
        });
        context.RegisterSourceOutput(enumModelProvider, static (sourceProductionContext, enumModel) =>
        {
            var cancellationToken = sourceProductionContext.CancellationToken;

            SourceGenerationHelper.AddSource(
                context: sourceProductionContext,
                extensionClassModel: enumModel.Generation.ExtensionClass,
                hintName: enumModel.Generation.HintName,
                methodModels: ExtensionMethodModelFactory.Create(enumModel, cancellationToken),
                cancellationToken
            );
        });
    }
    #endregion
}
