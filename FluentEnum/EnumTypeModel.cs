using System.Collections.Immutable;

namespace Macaron.FluentEnum;

public readonly record struct EnumTypeModel(
    string Type,
    string GenericParameters,
    ImmutableArray<string> GenericParameterConstraints
);
