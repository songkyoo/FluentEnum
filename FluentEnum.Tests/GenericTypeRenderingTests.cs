using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum.Tests;

[TestFixture]
public class GenericTypeRenderingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void RemapsConstraintsBySymbolAcrossNestedScopes(bool fluentOf)
    {
        var source = """
            using Macaron.FluentEnum;
            public interface IMarker<T> { }
            public class Container<T> { public interface INested<U> { } }
            public class Outer<T, U> where T : IMarker<T> where U : T
            {
                public class Inner<T, V> where T : IMarker<T> where V : U,
                    Container<U>.INested<(T Value, U[] Items)>, IMarker<T?[,]>
                {
                    ATTRIBUTE
                    public enum State { Ready }
                }
            }
            EXTENSIONS
            """;
        var generated = Generate(source, fluentOf, "Outer<,>.Inner<,>.State");

        Assert.Multiple(() =>
        {
            Assert.That(generated, Does.Contain("Outer<T0, T1>.Inner<T2, T3>.State"));
            Assert.That(generated, Does.Contain("where T0 : global::IMarker<T0>"));
            Assert.That(generated, Does.Contain("where T1 : T0"));
            Assert.That(generated, Does.Contain("where T2 : global::IMarker<T2>"));
            Assert.That(generated, Does.Contain("where T3 : T1, global::Container<T1>.INested<(T2 Value, T1[] Items)>, global::IMarker<T2?[,]>"));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void EscapesIdentifiersWithAndWithoutParameterRenaming(bool fluentOf, bool duplicate)
    {
        var source = """
            using Macaron.FluentEnum;
            namespace @namespace;
            public interface @interface<T> { }
            public class @class<@event> where @event : @interface<@event>
            {
                public class @struct<INNER> where INNER : @interface<INNER>
                {
                    ATTRIBUTE
                    public enum @enum { Ready }
                }
            }
            EXTENSIONS
            """.Replace("INNER", duplicate ? "@event" : "@return");
        var generated = Generate(source, fluentOf, "@class<>.@struct<>.@enum");
        var outer = duplicate ? "T0" : "@event";
        var inner = duplicate ? "T1" : "@return";

        Assert.Multiple(() =>
        {
            Assert.That(generated, Does.Contain($"global::@namespace.@class<{outer}>.@struct<{inner}>.@enum"));
            Assert.That(generated, Does.Contain($"Is<{outer}, {inner}>"));
            Assert.That(generated, Does.Contain($"where {outer} : global::@namespace.@interface<{outer}>"));
            Assert.That(generated, Does.Contain($"where {inner} : global::@namespace.@interface<{inner}>"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OmitsEmptyConstraintsAfterRenaming(bool fluentOf)
    {
        var generated = Generate("""
            using Macaron.FluentEnum;
            public class Outer<T> { public class Inner<T> {
                ATTRIBUTE
                public enum State { Ready }
            } }
            EXTENSIONS
            """, fluentOf, "Outer<>.Inner<>.State");

        Assert.That(generated, Does.Contain("Is<T0, T1>"));
        Assert.That(generated, Does.Not.Contain("where "));
        Assert.That(generated.ReplaceLineEndings("\n"), Does.Not.Contain("\n        \n"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemapsForwardReferencesAndGenericBaseConstraints(bool fluentOf)
    {
        var generated = Generate("""
            using Macaron.FluentEnum;
            public class Base<T> { }
            public class Outer<T, U> where T : U where U : class
            {
                public class Inner<T> where T : Base<U>, new()
                {
                    ATTRIBUTE
                    public enum State { Ready }
                }
            }
            EXTENSIONS
            """, fluentOf, "Outer<,>.Inner<>.State");

        Assert.Multiple(() =>
        {
            Assert.That(generated, Does.Contain("where T0 : T1"));
            Assert.That(generated, Does.Contain("where T1 : class"));
            Assert.That(generated, Does.Contain("where T2 : global::Base<T1>, new()"));
        });
    }

    [Test]
    public void PreservesClosedTypesWithEscapedIdentifiers()
    {
        var generated = Generate("""
            using Macaron.FluentEnum;
            namespace @namespace;
            public class @class<T> { public class @struct<T> {
                public enum @enum { Ready }
            } }
            EXTENSIONS
            """, true, "@class<string>.@struct<int>.@enum");

        Assert.That(generated, Does.Contain("global::@namespace.@class<string>.@struct<int>.@enum"));
        Assert.That(generated, Does.Not.Contain("Is<"));
        Assert.That(generated, Does.Not.Contain("where "));
    }

    private static string Generate(string source, bool fluentOf, string target)
    {
        source = source.Replace("ATTRIBUTE", fluentOf ? "" : "[Fluent]")
            .Replace("EXTENSIONS", fluentOf
                ? $"[FluentOf(typeof({target}))] public static partial class Extensions {{ }}"
                : "");
        Assert.That(Helper.CreateCompilation(source).GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty, "Input compilation");

        var (diagnostics, generated) = Helper.CompileAndGetResults(source);
        Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty,
            "Generated compilation");
        Assert.That(generated, Is.Not.Empty);
        return generated;
    }
}
