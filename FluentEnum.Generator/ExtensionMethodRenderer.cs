using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal static class ExtensionMethodRenderer
{
    public static ImmutableArray<string> Render(
        ImmutableArray<ExtensionMethodModel> methodModels,
        string indent,
        CancellationToken cancellationToken
    )
    {
        var lines = ImmutableArray.CreateBuilder<string>();

        for (var i = 0; i < methodModels.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var methodModel = methodModels[i];

            if (i > 0)
            {
                lines.Add("");
            }

            var parameters = string.Join(
                ", ",
                methodModel.Parameters.Select(static parameterModel =>
                    $"{(parameterModel.IsExtensionReceiver ? "this " : "")}{(parameterModel.IsParams ? "params " : "")}{parameterModel.Type} {parameterModel.Name}"
                )
            );

            lines.Add($"public static {methodModel.ReturnType} {methodModel.Name}{methodModel.GenericParameters}({parameters})");

            foreach (var constraint in methodModel.GenericParameterConstraints)
            {
                lines.Add($"{indent}{constraint}");
            }

            lines.Add("{");

            foreach (var bodyLine in methodModel.Body.Split('\n'))
            {
                lines.Add($"{indent}{bodyLine}");
            }

            lines.Add("}");
        }

        return lines.ToImmutable();
    }
}
