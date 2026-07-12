using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal abstract record AnalysisResult<TContext>
    where TContext : class
{
    internal sealed record Success(ImmutableArray<TContext> Contexts) : AnalysisResult<TContext>;

    internal sealed record Failure(ImmutableArray<Diagnostic> Diagnostics) : AnalysisResult<TContext>;
}
