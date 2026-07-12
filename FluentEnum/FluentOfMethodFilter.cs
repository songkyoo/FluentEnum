using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.FluentEnum;

internal static class FluentOfMethodFilter
{
    public static (
        ImmutableArray<GeneratedMethod> Methods,
        ImmutableArray<Diagnostic> Diagnostics
    ) Filter(
        INamedTypeSymbol classSymbol,
        EnumContext enumContext,
        ImmutableArray<GeneratedMethod> methods
    )
    {
        var filteredMethods = ImmutableArray.CreateBuilder<GeneratedMethod>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var method in methods)
        {
            var existingMembers = classSymbol.GetMembers(method.Name);
            var hasConflict = false;
            var hasEquivalentMethod = false;

            foreach (var existingMember in existingMembers)
            {
                if (existingMember is not IMethodSymbol existingMethod)
                {
                    AddConflictDiagnostic(
                        diagnostics,
                        classSymbol,
                        existingMember,
                        method
                    );
                    hasConflict = true;

                    continue;
                }

                if (!HasSameCSharpSignature(existingMethod, method, enumContext))
                {
                    continue;
                }

                if (IsEquivalentExtensionMethod(existingMethod, method, enumContext))
                {
                    hasEquivalentMethod = true;
                }
                else
                {
                    AddConflictDiagnostic(
                        diagnostics,
                        classSymbol,
                        existingMember,
                        method
                    );
                    hasConflict = true;
                }
            }

            if (!hasConflict && !hasEquivalentMethod)
            {
                filteredMethods.Add(method);
            }
        }

        return (filteredMethods.ToImmutable(), diagnostics.ToImmutable());
    }

    private static void AddConflictDiagnostic(
        ImmutableArray<Diagnostic>.Builder diagnostics,
        INamedTypeSymbol classSymbol,
        ISymbol existingMember,
        GeneratedMethod generatedMethod
    )
    {
        diagnostics.Add(Diagnostic.Create(
            descriptor: Diagnostics.ConflictingClassMember,
            location: existingMember.Locations.FirstOrDefault(),
            messageArgs: [
                existingMember.Name,
                classSymbol.ToDisplayString(),
                generatedMethod.Name,
            ]
        ));
    }

    private static bool HasSameCSharpSignature(
        IMethodSymbol existingMethod,
        GeneratedMethod generatedMethod,
        EnumContext enumContext
    )
    {
        if (existingMethod.Arity != generatedMethod.GenericArity
            || existingMethod.Parameters.Length != generatedMethod.Parameters.Length
        )
        {
            return false;
        }

        foreach (var existingParameter in existingMethod.Parameters)
        {
            if (existingParameter.RefKind != RefKind.None
                || !IsTargetType(existingParameter.Type, existingMethod, enumContext)
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTargetType(
        ITypeSymbol type,
        IMethodSymbol existingMethod,
        EnumContext enumContext
    )
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (enumContext.TargetKind == EnumTargetKind.Closed)
        {
            return SymbolEqualityComparer.Default.Equals(namedType, enumContext.Symbol);
        }

        var symbolComparer = SymbolEqualityComparer.Default;

        if (!symbolComparer.Equals(
            namedType.OriginalDefinition,
            enumContext.Symbol.OriginalDefinition
        ))
        {
            return false;
        }

        var typeArguments = SymbolHelpers
            .GetNestedTypeSymbols(namedType)
            .SelectMany(static symbol => symbol.TypeArguments)
            .ToImmutableArray();

        if (typeArguments.Length != existingMethod.TypeParameters.Length)
        {
            return false;
        }

        return !typeArguments
            .Where((symbol, i) =>
                !symbolComparer.Equals(symbol, existingMethod.TypeParameters[i])
            )
            .Any();
    }

    private static bool IsEquivalentExtensionMethod(
        IMethodSymbol method,
        GeneratedMethod generatedMethod,
        EnumContext enumContext
    )
    {
        if (method is not
        {
            MethodKind: MethodKind.Ordinary,
            IsStatic: true,
            IsExtensionMethod: true,
            DeclaredAccessibility: Accessibility.Public,
            ReturnType.SpecialType: SpecialType.System_Boolean,
            ReturnsByRef: false,
            ReturnsByRefReadonly: false,
        })
        {
            return false;
        }

        if (!method
            .Parameters
            .Zip(
                generatedMethod.Parameters,
                static (actual, _) => actual is
                {
                    IsParams: false,
                    IsOptional: false,
                    HasExplicitDefaultValue: false,
                }
            )
            .All(static isEquivalent => isEquivalent)
        )
        {
            return false;
        }

        return HasEquivalentTypeParameterConstraints(method, enumContext);
    }

    private static bool HasEquivalentTypeParameterConstraints(
        IMethodSymbol method,
        EnumContext enumContext
    )
    {
        if (enumContext.TargetKind == EnumTargetKind.Closed)
        {
            return method.TypeParameters.IsEmpty;
        }

        var expectedTypeParameters = SymbolHelpers
            .GetNestedTypeSymbols(enumContext.Symbol.OriginalDefinition)
            .SelectMany(static symbol => symbol.TypeParameters)
            .ToImmutableArray();

        if (expectedTypeParameters.Length != method.TypeParameters.Length)
        {
            return false;
        }

        for (var i = 0; i < expectedTypeParameters.Length; i++)
        {
            var expected = expectedTypeParameters[i];
            var actual = method.TypeParameters[i];

            if (expected.HasReferenceTypeConstraint != actual.HasReferenceTypeConstraint
                || expected.ReferenceTypeConstraintNullableAnnotation != actual.ReferenceTypeConstraintNullableAnnotation
                || expected.HasValueTypeConstraint != actual.HasValueTypeConstraint
                || expected.HasUnmanagedTypeConstraint != actual.HasUnmanagedTypeConstraint
                || expected.HasConstructorConstraint != actual.HasConstructorConstraint
                || expected.HasNotNullConstraint != actual.HasNotNullConstraint
                || expected.ConstraintTypes.Length != actual.ConstraintTypes.Length
            )
            {
                return false;
            }

            for (
                var constraintIndex = 0;
                constraintIndex < expected.ConstraintTypes.Length;
                constraintIndex++
            )
            {
                if (!AreEquivalentConstraintTypes(
                    expected.ConstraintTypes[constraintIndex],
                    actual.ConstraintTypes[constraintIndex],
                    expectedTypeParameters,
                    method.TypeParameters
                ))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AreEquivalentConstraintTypes(
        ITypeSymbol expected,
        ITypeSymbol actual,
        ImmutableArray<ITypeParameterSymbol> expectedTypeParameters,
        ImmutableArray<ITypeParameterSymbol> actualTypeParameters
    )
    {
        var symbolComparer = SymbolEqualityComparer.Default;

        switch (expected)
        {
            case ITypeParameterSymbol expectedTypeParameter:
            {
                for (var i = 0; i < expectedTypeParameters.Length; i++)
                {
                    if (symbolComparer.Equals(
                        expectedTypeParameter,
                        expectedTypeParameters[i]
                    ))
                    {
                        return
                            actual is ITypeParameterSymbol actualTypeParameter
                            && symbolComparer.Equals(
                                actualTypeParameter,
                                actualTypeParameters[i]
                            );
                    }
                }

                return false;
            }
            case INamedTypeSymbol expectedNamedType when actual is INamedTypeSymbol actualNamedType:
            {
                if (!symbolComparer.Equals(
                        expectedNamedType.OriginalDefinition,
                        actualNamedType.OriginalDefinition
                    )
                    || expectedNamedType.TypeArguments.Length != actualNamedType.TypeArguments.Length
                )
                {
                    return false;
                }

                for (var i = 0; i < expectedNamedType.TypeArguments.Length; i++)
                {
                    if (!AreEquivalentConstraintTypes(
                        expectedNamedType.TypeArguments[i],
                        actualNamedType.TypeArguments[i],
                        expectedTypeParameters,
                        actualTypeParameters
                    ))
                    {
                        return false;
                    }
                }

                return true;
            }
            default:
                return SymbolEqualityComparer.IncludeNullability.Equals(expected, actual);
        }
    }
}
