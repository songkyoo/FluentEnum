using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal static class ExtensionMethodFactory
{
    public static ImmutableArray<GeneratedMethod> Create(EnumContext enumContext)
    {
        var (symbol, _, members, generateNegatedMembers, hasFlags, targetKind) = enumContext;
        var generatedType = GeneratedEnumTypeFactory.Create(symbol, targetKind);
        var type = generatedType.Type;
        var escapedSymbolName = NamingHelpers.GetEscapedKeyword(
            NamingHelpers.GetCamelCaseName(symbol.Name)
        );
        var memberSet = members
            .Select(static member => member.Name)
            .ToImmutableHashSet();

        var methods = ImmutableArray.CreateBuilder<GeneratedMethod>();
        var receiverParameter = new GeneratedParameter(
            Type: type,
            Name: escapedSymbolName,
            IsExtensionReceiver: true
        );

        methods.Add(CreateMethod(
            name: "Is",
            parameters: ImmutableArray.Create(
                receiverParameter,
                new GeneratedParameter(type, "value")
            ),
            body: $"return {escapedSymbolName} == value;",
            generatedType
        ));

        methods.Add(CreateMethod(
            name: "IsNot",
            parameters: ImmutableArray.Create(
                receiverParameter,
                new GeneratedParameter(type, "value")
            ),
            body: $"return {escapedSymbolName} != value;",
            generatedType
        ));

        foreach (var member in members)
        {
            methods.Add(CreateMethod(
                name: $"Is{member.Name}",
                parameters: ImmutableArray.Create(receiverParameter),
                body: $"return {escapedSymbolName} == {type}.{member.Name};",
                generatedType
            ));
        }

        if (generateNegatedMembers)
        {
            foreach (var member in members)
            {
                var negatedMember = GetNegatedMemberName(member.Name);

                if (memberSet.Contains(negatedMember))
                {
                    continue;
                }

                methods.Add(CreateMethod(
                    name: $"Is{negatedMember}",
                    parameters: ImmutableArray.Create(receiverParameter),
                    body: $"return {escapedSymbolName} != {type}.{member.Name};",
                    generatedType
                ));
            }
        }

        if (hasFlags)
        {
            methods.Add(CreateMethod(
                name: "Has",
                parameters: ImmutableArray.Create(
                    receiverParameter,
                    new GeneratedParameter(type, "value")
                ),
                body: $"return ({escapedSymbolName} & value) == value;",
                generatedType
            ));

            methods.Add(CreateMethod(
                name: "HasNot",
                parameters: ImmutableArray.Create(
                    receiverParameter,
                    new GeneratedParameter(type, "value")
                ),
                body: $"return ({escapedSymbolName} & value) != value;",
                generatedType
            ));

            foreach (var member in members.Where(static member => !IsZero(member.Value)))
            {
                methods.Add(CreateMethod(
                    name: $"Has{member.Name}",
                    parameters: ImmutableArray.Create(receiverParameter),
                    body: $"return ({escapedSymbolName} & {type}.{member.Name}) == {type}.{member.Name};",
                    generatedType
                ));
            }

            if (generateNegatedMembers)
            {
                foreach (var member in members.Where(static member => !IsZero(member.Value)))
                {
                    var negatedMember = GetNegatedMemberName(member.Name);

                    if (memberSet.Contains(negatedMember))
                    {
                        continue;
                    }

                    methods.Add(CreateMethod(
                        name: $"HasNot{member.Name}",
                        parameters: ImmutableArray.Create(receiverParameter),
                        body: $"return ({escapedSymbolName} & {type}.{member.Name}) != {type}.{member.Name};",
                        generatedType
                    ));
                }
            }
        }

        return methods.ToImmutable();
    }

    private static GeneratedMethod CreateMethod(
        string name,
        ImmutableArray<GeneratedParameter> parameters,
        string body,
        GeneratedEnumType generatedType
    )
    {
        return new GeneratedMethod(
            Name: name,
            GenericParameters: generatedType.GenericParameters,
            GenericArity: generatedType.GenericArity,
            GenericParameterConstraints: generatedType.GenericParameterConstraints,
            Parameters: parameters,
            Body: body
        );
    }

    private static string GetNegatedMemberName(string member)
    {
        return member.StartsWith("Not") ? member[3..] : $"Not{member}";
    }

    private static bool IsZero(object value)
    {
        return value switch
        {
            byte typedValue => typedValue == 0,
            sbyte typedValue => typedValue == 0,
            short typedValue => typedValue == 0,
            ushort typedValue => typedValue == 0,
            int typedValue => typedValue == 0,
            uint typedValue => typedValue == 0,
            long typedValue => typedValue == 0,
            ulong typedValue => typedValue == 0,
            _ => false,
        };
    }
}
