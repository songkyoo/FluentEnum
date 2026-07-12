using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal sealed record GeneratedMethod(
    string Name,
    string GenericParameters,
    ImmutableArray<string> GenericParameterConstraints,
    ImmutableArray<GeneratedParameter> Parameters,
    string Body
);
