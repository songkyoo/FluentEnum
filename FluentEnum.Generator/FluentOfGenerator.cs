using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Macaron.FluentEnum;

[Generator]
public class FluentOfGenerator : IIncrementalGenerator
{
    #region Constants
    private const string FluentOfAttributeMetadataName = "Macaron.FluentEnum.FluentOfAttribute";
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analysisResult = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: FluentOfAttributeMetadataName,
                predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
                transform: static (generatorAttributeSyntaxContext, cancellationToken) => AnalysisModelFactory.GetFluentOfModel(
                    generatorAttributeSyntaxContext,
                    cancellationToken
                )
            )
            .Where(static result => result != null)
            .Select(static (result, _) => result!);
        var diagnosticProvider = analysisResult
            .Where(static result => result is AnalysisResult<FluentOfModel>.Failure)
            .SelectMany(static (result, _) => ((AnalysisResult<FluentOfModel>.Failure)result).Diagnostics);
        var fluentOfModelProvider = analysisResult
            .Where(static result => result is AnalysisResult<FluentOfModel>.Success)
            .Select(static (result, _) => ((AnalysisResult<FluentOfModel>.Success)result).Model)
            .WithComparer(FluentOfModelComparer.Instance);

        context.RegisterSourceOutput(diagnosticProvider, static (sourceProductionContext, diagnostic) =>
        {
            sourceProductionContext.ReportDiagnostic(diagnostic);
        });
        context.RegisterSourceOutput(fluentOfModelProvider, static (sourceProductionContext, fluentOfModel) =>
        {
            var cancellationToken = sourceProductionContext.CancellationToken;

            SourceGenerationHelper.AddSource(
                context: sourceProductionContext,
                extensionClassModel: fluentOfModel.ExtensionClass,
                hintName: fluentOfModel.Enum.Generation.HintName,
                methodModels: ExtensionMethodModelFactory.Create(fluentOfModel.Enum, cancellationToken),
                cancellationToken
            );
        });
    }
    #endregion
}
