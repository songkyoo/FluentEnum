using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor InvalidEnumAccessibility = new(
        id: "MAFE0001",
        title: "Unsupported enum accessibility",
        messageFormat: "Enum '{0}' is declared with unsupported accessibility and cannot be processed by generator.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor InvalidFluentOfClass = new(
        id: "MAFE0002",
        title: "Unsupported FluentOf class",
        messageFormat: "Class '{0}' must be a non-generic, top-level static partial class to use FluentOf.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor InvalidFluentOfTarget = new(
        id: "MAFE0003",
        title: "Unsupported FluentOf target",
        messageFormat: "FluentOf on class '{0}' must reference an enum type.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor ConflictingClassMember = new(
        id: "MAFE0004",
        title: "Class member conflicts with generated extension method",
        messageFormat: "Member '{0}' in class '{1}' conflicts with generated extension method '{2}'.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
