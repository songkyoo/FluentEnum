namespace Macaron.FluentEnum;

public sealed record EnumGenerationModel(
    ExtensionClassModel ExtensionClass,
    string HintName,
    string ReceiverName,
    EnumTypeModel EnumType
);
