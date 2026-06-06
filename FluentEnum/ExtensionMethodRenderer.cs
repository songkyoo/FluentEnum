using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal static class ExtensionMethodRenderer
{
    public static ImmutableArray<string> Render(ImmutableArray<GeneratedMethod> methods, string indent)
    {
        var lines = ImmutableArray.CreateBuilder<string>();

        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];

            if (i > 0)
            {
                lines.Add("");
            }

            var parameters = string.Join(
                ", ",
                method.Parameters.Select(static parameter =>
                    $"{(parameter.IsExtensionReceiver ? "this " : "")}{parameter.Type} {parameter.Name}"
                )
            );

            lines.Add($"public static bool {method.Name}{method.GenericParameters}({parameters})");

            foreach (var constraint in method.GenericParameterConstraints)
            {
                lines.Add($"{indent}{constraint}");
            }

            lines.Add("{");
            lines.Add($"{indent}{method.Body}");
            lines.Add("}");
        }

        return lines.ToImmutable();
    }
}
