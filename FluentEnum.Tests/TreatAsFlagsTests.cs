using Microsoft.CodeAnalysis;
using static Macaron.FluentEnum.Tests.Helper;

namespace Macaron.FluentEnum.Tests;

[TestFixture]
public class TreatAsFlagsTests
{
    [TestCase(false, false, null, false)]
    [TestCase(false, false, false, false)]
    [TestCase(false, false, true, true)]
    [TestCase(false, true, null, true)]
    [TestCase(false, true, false, true)]
    [TestCase(false, true, true, true)]
    [TestCase(true, false, null, false)]
    [TestCase(true, false, false, false)]
    [TestCase(true, false, true, true)]
    [TestCase(true, true, null, true)]
    [TestCase(true, true, false, true)]
    [TestCase(true, true, true, true)]
    public void Should_SelectFlagGeneration_UsingAttributeOrOption(
        bool fluentOf, bool flagsAttribute, bool? treatAsFlags, bool expectedFlags
    )
    {
        var option = treatAsFlags is { } value ? $"TreatAsFlags = {value.ToString().ToLowerInvariant()}" : "";
        var attribute = fluentOf
            ? $"[FluentOf(typeof(Permission){(option.Length > 0 ? ", " + option : "")})]"
            : $"[Fluent({option})]";
        var (diagnostics, code) = CompileAndGetResults($$"""
            using System;
            using Macaron.FluentEnum;

            {{(flagsAttribute ? "[Flags]" : "")}}
            {{(fluentOf ? "" : attribute)}}
            public enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2,
                Both = Read | Write,
            }

            {{(fluentOf ? attribute + " public static partial class PermissionExtensions { }" : "")}}
            """);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Severity == DiagnosticSeverity.Error));
            Assert.That(code, Does.Contain("public static bool Is("));
            Assert.That(code.Contains("public static bool Has("), Is.EqualTo(expectedFlags));
            Assert.That(code.Contains("public static bool HasNot("), Is.EqualTo(expectedFlags));
            Assert.That(code.Contains("public static bool HasRead("), Is.EqualTo(expectedFlags));
            Assert.That(code.Contains("public static bool HasNotRead("), Is.EqualTo(expectedFlags));
            Assert.That(code.Contains("public static bool HasBoth("), Is.EqualTo(expectedFlags));
            Assert.That(code, Does.Not.Contain("public static bool HasNone("));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Should_RespectNegatedMemberOption_When_TreatedAsFlags(bool fluentOf)
    {
        const string options = "TreatAsFlags = true, GenerateNegatedMembers = false";
        var (diagnostics, code) = CompileAndGetResults($$"""
            using Macaron.FluentEnum;

            {{(fluentOf ? "" : $"[Fluent({options})]")}}
            public enum Permission { None = 0, Read = 1 }

            {{(fluentOf ? $"[FluentOf(typeof(Permission), {options})] public static partial class PermissionExtensions {{ }}" : "")}}
            """);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Severity == DiagnosticSeverity.Error));
            Assert.That(code, Does.Contain("public static bool HasRead("));
            Assert.That(code, Does.Contain("public static bool HasNot("));
            Assert.That(code, Does.Not.Contain("public static bool HasNotRead("));
            Assert.That(code, Does.Not.Contain("public static bool IsNotRead("));
        });
    }
}
