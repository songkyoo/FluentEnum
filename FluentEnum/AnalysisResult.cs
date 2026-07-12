using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal abstract record AnalysisResult<TModel>
    where TModel : class
{
    internal sealed record Success(TModel Model) : AnalysisResult<TModel>;

    internal sealed record Failure(ImmutableArray<Diagnostic> Diagnostics) : AnalysisResult<TModel>;
}
