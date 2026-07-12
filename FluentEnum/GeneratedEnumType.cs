using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal readonly record struct GeneratedEnumType(
    string Type,
    string GenericParameters,
    ImmutableArray<string> GenericParameterConstraints
);
