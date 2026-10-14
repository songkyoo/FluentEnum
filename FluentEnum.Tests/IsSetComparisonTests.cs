using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using static Macaron.FluentEnum.Tests.Helper;

namespace Macaron.FluentEnum.Tests;

[TestFixture]
public class IsSetComparisonTests
{
    [Test]
    public void Should_CompareAgainstMultipleValues(
        [Values] bool fluentOf,
        [Values] bool flags
    )
    {
        var source =
            $$"""
            using System;
            using Macaron.FluentEnum;

            {{(flags ? "[Flags]" : "")}}
            {{(fluentOf ? "" : "[Fluent]")}}
            public enum State { None = 0, Ready = 1, Running = 2, Done = 4 }

            {{(fluentOf ? "[FluentOf(typeof(State))] public static partial class StateExtensions { }" : "")}}

            public static class Probe
            {
                public static bool[] Query()
                {
                    var state = State.Ready;
                    return new[]
                    {
                        state.IsAny(State.None, State.Ready),
                        state.IsNone(State.None, State.Running),
                        state.IsAny(State.None, State.Running, State.Done),
                        state.IsNone(State.None, State.Running, State.Done),
                        state.IsAny(State.None, State.Running, State.Done, State.Ready),
                        state.IsNone(State.None, State.Running, State.Done, State.Ready),
                        state.IsAny(State.None, State.Running, State.Done, State.None, State.Ready),
                        state.IsNone(State.None, State.Running, State.Done, State.None, State.Ready),
                        state.IsAny(State.None, State.Running, State.Done, State.None, State.Running, State.Ready),
                        state.IsNone(State.None, State.Running, State.Done, State.None, State.Running, State.Ready),
                        state.IsAny(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.Ready),
                        state.IsNone(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.Ready),
                        state.IsAny(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.None, State.Ready),
                        state.IsNone(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.None, State.Ready),
                        state.IsAny(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.None, State.Running, State.Ready),
                        state.IsNone(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.None, State.Running, State.Ready),
                        State.Ready.IsAny(State.Running, State.Done),
                        State.Ready.IsNone(State.Ready, State.Ready),
                        (State.Ready | State.Running).IsAny(State.Ready, State.Running),
                        (State.Ready | State.Running).IsNone(State.Ready, State.Running),
                    };
                }
            }
            """;
        var driver = CSharpGeneratorDriver
            .Create(new FluentGenerator(), new FluentOfGenerator())
            .RunGeneratorsAndUpdateCompilation(
                CreateCompilation(source),
                out var output,
                out var diagnostics
            );

        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Severity == DiagnosticSeverity.Error));

        using var stream = new MemoryStream();
        var emit = output.Emit(stream);

        Assert.That(emit.Success, Is.True, string.Join(Environment.NewLine, emit.Diagnostics));

        var query = Assembly.Load(stream.ToArray()).GetType("Probe")!.GetMethod("Query")!;

        Assert.That(query.Invoke(null, null), Is.EqualTo(new[]
        {
            true,  // state.IsAny(State.None, State.Ready),
            true,  // state.IsNone(State.None, State.Running),
            false, // state.IsAny(State.None, State.Running, State.Done),
            true,  // state.IsNone(State.None, State.Running, State.Done),
            true,  // state.IsAny(State.None, State.Running, State.Done, State.Ready),
            false, // state.IsNone(State.None, State.Running, State.Done, State.Ready),
            true,  // state.IsAny(State.None, State.Running, State.Done, State.None, State.Ready),
            false, // state.IsNone(State.None, State.Running, State.Done, State.None, State.Ready),
            true,  // state.IsAny(State.None, State.Running, State.Done, State.None, State.Running, State.Ready),
            false, // state.IsNone(State.None, State.Running, State.Done, State.None, State.Running, State.Ready),
            true,  // state.IsAny(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.Ready),
            false, // state.IsNone(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.Ready),
            true,  // state.IsAny(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.None, State.Ready),
            false, // state.IsNone(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.None, State.Ready),
            true,  // state.IsAny(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.None, State.Running, State.Ready),
            false, // state.IsNone(State.None, State.Running, State.Done, State.None, State.Running, State.Done, State.None, State.Running, State.Ready),
            false, // State.Ready.IsAny(State.Running, State.Done),
            false, // State.Ready.IsNone(State.Ready, State.Ready),
            false, // (State.Ready | State.Running).IsAny(State.Ready, State.Running),
            true,  // (State.Ready | State.Running).IsNone(State.Ready, State.Running),
        }));

        var code = string.Join(
            "\n",
            driver
                .GetRunResult()
                .Results
                .SelectMany(result => result.GeneratedSources)
                .Select(result => result.SourceText.ToString())
        );

        Assert.That(code, Does.Contain("bool IsAny("));
        Assert.That(code, Does.Contain("bool IsNone("));
        Assert.That(code, Does.Contain("params global::State[] values"));
    }

    [TestCase("fluent")]
    [TestCase("open")]
    [TestCase("closed")]
    public void Should_CompileForNestedGenericEnum(string target)
    {
        var (diagnostics, _) = CompileAndGetResults(
            $$"""
            using Macaron.FluentEnum;

            public class Outer<T> where T : class
            {
                {{(target == "fluent" ? "[Fluent]" : "")}}
                public enum State { Ready, Running }
            }

            {{(target == "fluent" ? "" : $"[FluentOf(typeof(Outer<{(target == "closed" ? "object" : "")}>.State))] public static partial class Extensions {{ }}")}}

            public static class Probe
            {
                public static bool Query(Outer<object>.State state)
                    => state.IsAny(Outer<object>.State.Ready, Outer<object>.State.Running)
                        && !state.IsNone(Outer<object>.State.Ready, Outer<object>.State.Running)
                        && state.IsAny(Outer<object>.State.Ready, Outer<object>.State.Running,
                            Outer<object>.State.Ready, Outer<object>.State.Running,
                            Outer<object>.State.Ready, Outer<object>.State.Running,
                            Outer<object>.State.Ready, Outer<object>.State.Running,
                            Outer<object>.State.Ready);
            }
            """
        );

        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Severity == DiagnosticSeverity.Error));
    }
}
