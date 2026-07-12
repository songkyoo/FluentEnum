using System.Runtime.CompilerServices;

namespace Macaron.FluentEnum;

internal sealed class AnalysisResultComparer<TContext>(IEqualityComparer<TContext> contextComparer)
    : IEqualityComparer<AnalysisResult<TContext>>
    where TContext : class
{
    public bool Equals(AnalysisResult<TContext>? x, AnalysisResult<TContext>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x is AnalysisResult<TContext>.Success xSuccess
            && y is AnalysisResult<TContext>.Success ySuccess
            && ImmutableArrayComparer.Equals(
                xSuccess.Contexts,
                ySuccess.Contexts,
                contextComparer
            );
    }

    public int GetHashCode(AnalysisResult<TContext> obj)
    {
        return obj is AnalysisResult<TContext>.Success success
            ? ImmutableArrayComparer.GetHashCode(success.Contexts, contextComparer)
            : RuntimeHelpers.GetHashCode(obj);
    }
}
