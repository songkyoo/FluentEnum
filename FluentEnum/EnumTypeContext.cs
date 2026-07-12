namespace Macaron.FluentEnum;

internal sealed record EnumTypeContext(
    ExtensionClassContext ExtensionClassContext,
    string HintName,
    string ReceiverName,
    GeneratedEnumType GeneratedType
);
