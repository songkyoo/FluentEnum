namespace Macaron.FluentEnum;

internal sealed record EnumTypeContext(
    string Namespace,
    string ExtensionClassName,
    string AccessModifier,
    string HintName,
    string ReceiverName,
    GeneratedEnumType GeneratedType
);
