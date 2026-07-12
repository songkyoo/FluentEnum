using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Macaron.FluentEnum.Tests;

public static class Helper
{
    public static void AssertGeneratedCode(
        string sourceCode,
        string expected,
        out ImmutableArray<Diagnostic> diagnostics
    )
    {
        (diagnostics, var generatedCode) = CompileAndGetResults(sourceCode);

        Assert.That(generatedCode.ReplaceLineEndings(), Is.EqualTo(expected.ReplaceLineEndings()));
    }

    public static void AssertGeneratedCode(string sourceCode, string expected)
    {
        var (_, generatedCode) = CompileAndGetResults(sourceCode);

        Assert.That(generatedCode.ReplaceLineEndings(), Is.EqualTo(expected.ReplaceLineEndings()));
    }

    public static (ImmutableArray<Diagnostic> diagnostics, string generatedCode) CompileAndGetResults(string sourceCode)
    {
        var compilation = CreateCompilation(sourceCode);
        var driver = CSharpGeneratorDriver
            .Create(
                new FluentEnumExtensionsGenerator(),
                new FluentOfEnumExtensionsGenerator()
            )
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics
            );

        var generatedCode = string.Join(
            Environment.NewLine,
            driver
                .GetRunResult()
                .Results
                .SelectMany(static result => result.GeneratedSources)
                .Where(static generatedSource =>
                {
                    return generatedSource.HintName is not ("FluentAttribute.g.cs" or "FluentOfAttribute.g.cs");
                })
                .Select(static generatedSource => generatedSource.SourceText.ToString())
        );
        var diagnostics = outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .ToImmutableArray();

        return (diagnostics, generatedCode);
    }

    public static CSharpCompilation CreateCompilation(string sourceCode)
    {
        var references = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        return CSharpCompilation.Create(
            assemblyName: "Macaron.PropertyAccessor.Tests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }
}
