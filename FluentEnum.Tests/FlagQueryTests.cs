using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using static Macaron.FluentEnum.Tests.Helper;

namespace Macaron.FluentEnum.Tests;

[TestFixture]
public class FlagQueryTests
{
    [Test]
    public void Should_EvaluateFlagQueries(
        [Values] bool fluentOf,
        [Values("byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong")] string underlyingType
    )
    {
        var source = $$"""
            using System;
            using Macaron.FluentEnum;
            {{(fluentOf ? "" : "[Flags, Fluent]")}}
            public enum Permission : {{underlyingType}}
            {
                Zero = 0,
                Read = 1,
                Any = 2,
                None = 3,
                High = unchecked(({{underlyingType}})(1UL << (sizeof({{underlyingType}}) * 8 - 1))),
            }
            {{(fluentOf ? "[FluentOf(typeof(Permission), TreatAsFlags = true, GenerateNegatedMembers = false)] public static partial class PermissionExtensions { }" : "")}}
            public static class Probe
            {
                public static bool[] Query(int receiver, int mask)
                {
                    var flags = (Permission)receiver;
                    var value = (Permission)mask;
                    return new[] { flags.Has(value), flags.HasNot(value), flags.HasAny(value), flags.HasNone(value) };
                }
                public static bool CheckOverloadsAndHighBit()
                {
                    return Permission.Any.HasAny()
                        && Permission.None.HasNone()
                        && Permission.High.HasAny(Permission.High)
                        && !Permission.High.HasNone(Permission.High)
                        && Permission.High.HasNone(Permission.Read);
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

        var probe = Assembly.Load(stream.ToArray()).GetType("Probe")!;
        var query = probe.GetMethod("Query")!;

        // Expected results are Has, HasNot, HasAny, HasNone, respectively.
        Assert.Multiple(() =>
        {
            Check(0, 0, true, false, false, true);
            Check(1, 0, true, false, false, true);
            Check(0, 3, false, true, false, true);
            Check(1, 3, false, true, true, false);
            Check(3, 3, true, false, true, false);
            Check(1, 2, false, true, false, true);
            Check(8, 8, true, false, true, false); // Undeclared bits remain meaningful.
            Assert.That(probe.GetMethod("CheckOverloadsAndHighBit")!.Invoke(null, null), Is.True);
            var code = string.Join("\n", driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
            Assert.That(code, Does.Not.Contain("HasAnyRead("));
            Assert.That(code, Does.Not.Contain("HasNoneRead("));
        });

        #region Local Functions
        void Check(int receiver, int mask, params bool[] expected)
        {
            Assert.That(query.Invoke(null, new object[] { receiver, mask }), Is.EqualTo(expected), $"receiver={receiver}, mask={mask}");
        }
        #endregion
    }

    [TestCase("fluent")]
    [TestCase("open")]
    [TestCase("closed")]
    public void Should_CompileQueriesForNestedGenericEnum(string target)
    {
        var (diagnostics, _) = CompileAndGetResults(
            $$"""
            using Macaron.FluentEnum;
            public class Outer<T> where T : class, new()
            {
                {{(target == "fluent" ? "[Fluent(TreatAsFlags = true)]" : "")}}
                public enum Permission { Read = 1 }
            }
            {{(target == "fluent" ? "" : $"[FluentOf(typeof(Outer<{(target == "closed" ? "object" : "")}>.Permission), TreatAsFlags = true)] public static partial class Extensions {{ }}")}}
            public static class Probe
            {
                public static bool Query(Outer<object>.Permission value)
                    => value.HasAny(Outer<object>.Permission.Read) && !value.HasNone(Outer<object>.Permission.Read);
            }
            """
        );

        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Severity == DiagnosticSeverity.Error));
    }
}
