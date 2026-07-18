using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal sealed record ExtensionMethodModel(
    string Name,
    string GenericParameters,
    ImmutableArray<string> GenericParameterConstraints,
    ImmutableArray<MethodParameterModel> Parameters,
    string Body
);
