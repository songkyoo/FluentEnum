using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using static Macaron.FluentEnum.Tests.Helper;

namespace Macaron.FluentEnum.Tests;

[TestFixture]
public class FlagOperationTests
{
    [Test]
    public void Should_ReturnUpdatedFlags(
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
                None = 0,
                Read = 1,
                Write = 2,
                High = unchecked(({{underlyingType}})(1UL << (sizeof({{underlyingType}}) * 8 - 1))),
                All = unchecked(({{underlyingType}})~0UL),
            }
            {{(fluentOf ? "[FluentOf(typeof(Permission), TreatAsFlags = true, GenerateNegatedMembers = false)] public static partial class PermissionExtensions { }" : "")}}
            public static class Probe
            {
                public static int[] Update(int receiver, int mask)
                {
                    var original = (Permission)receiver;
                    var value = (Permission)mask;
                    return new[] {
                        (int)original.Add(value), (int)original.Remove(value),
                        (int)original.Add(value).Add(value), (int)original.Remove(value).Remove(value),
                        (int)original,
                    };
                }
                public static bool[] HighBit()
                {
                    var combined = Permission.High.Add(Permission.Read);
                    return new[] {
                        combined == (Permission.High | Permission.Read),
                        combined.Remove(Permission.Read) == Permission.High,
                        combined.Remove(Permission.High) == Permission.Read,
                        Permission.None.Add(Permission.All) == Permission.All,
                        Permission.All.Remove(Permission.All) == Permission.None,
                        Permission.Read.Remove(Permission.All) == Permission.None,
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

        var probe = Assembly.Load(stream.ToArray()).GetType("Probe")!;
        var update = probe.GetMethod("Update")!;

        Assert.Multiple(() =>
        {
            Check(0, 0, 0, 0);
            Check(1, 0, 1, 1);
            Check(0, 3, 3, 0);
            Check(1, 3, 3, 0);
            Check(3, 1, 3, 2);
            Check(1, 2, 3, 1);
            Check(9, 3, 11, 8); // Preserve undeclared bits outside the mask.
            Assert.That(probe.GetMethod("HighBit")!.Invoke(null, null), Is.EqualTo(new[] { true, true, true, true, true, true }));

            var code = string.Join("\n", driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

            Assert.That(code, Does.Not.Contain("AddRead("));
            Assert.That(code, Does.Not.Contain("RemoveRead("));
            Assert.That(code, Does.Not.Contain(" Or("));
            Assert.That(code, Does.Not.Contain(" And("));
            Assert.That(code, Does.Not.Contain(" Xor("));
        });

        #region Local Functions
        void Check(int receiver, int mask, int added, int removed)
        {
            Assert.That(update.Invoke(null, new object[] { receiver, mask }),
                Is.EqualTo(new[] { added, removed, added, removed, receiver }), $"receiver={receiver}, mask={mask}");
        }
        #endregion
    }

    [TestCase("fluent")]
    [TestCase("open")]
    [TestCase("closed")]
    public void Should_ReturnNestedGenericEnum(string target)
    {
        var (diagnostics, _) = CompileAndGetResults(
            $$"""
            using Macaron.FluentEnum;
            public class Outer<T> where T : class, new()
            {
                {{(target == "fluent" ? "[Fluent(TreatAsFlags = true)]" : "")}}
                public enum Permission { Read = 1, Write = 2 }
            }
            {{(target == "fluent" ? "" : $"[FluentOf(typeof(Outer<{(target == "closed" ? "object" : "")}>.Permission), TreatAsFlags = true)] public static partial class Extensions {{ }}")}}
            public static class Probe
            {
                public static Outer<object>.Permission Update(Outer<object>.Permission value)
                    => value.Add(Outer<object>.Permission.Read).Remove(Outer<object>.Permission.Write);
            }
            """
        );

        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Severity == DiagnosticSeverity.Error));
    }
}
