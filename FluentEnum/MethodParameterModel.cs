namespace Macaron.FluentEnum;

internal readonly record struct MethodParameterModel(
    string Type,
    string Name,
    bool IsExtensionReceiver = false
);
