using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal static class ExtensionMethodRenderer
{
    public static ImmutableArray<string> Render(
        ImmutableArray<ExtensionMethodModel> methodModels,
        string indent
    )
    {
        var lines = ImmutableArray.CreateBuilder<string>();

        for (var i = 0; i < methodModels.Length; i++)
        {
            var methodModel = methodModels[i];

            if (i > 0)
            {
                lines.Add("");
            }

            var parameters = string.Join(
                ", ",
                methodModel.Parameters.Select(static parameterModel =>
                    $"{(parameterModel.IsExtensionReceiver ? "this " : "")}{parameterModel.Type} {parameterModel.Name}"
                )
            );

            lines.Add($"public static bool {methodModel.Name}{methodModel.GenericParameters}({parameters})");

            foreach (var constraint in methodModel.GenericParameterConstraints)
            {
                lines.Add($"{indent}{constraint}");
            }

            lines.Add("{");
            lines.Add($"{indent}{methodModel.Body}");
            lines.Add("}");
        }

        return lines.ToImmutable();
    }
}
