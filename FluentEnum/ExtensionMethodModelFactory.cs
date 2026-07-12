using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal static class ExtensionMethodModelFactory
{
    public static ImmutableArray<ExtensionMethodModel> Create(EnumModel enumModel)
    {
        var (generationModel, members, generateNegatedMembers, hasFlags) = enumModel;
        var enumTypeModel = generationModel.EnumType;
        var type = enumTypeModel.Type;
        var receiverName = generationModel.ReceiverName;
        var memberSet = members
            .Select(static member => member.Name)
            .ToImmutableHashSet();

        var methodModels = ImmutableArray.CreateBuilder<ExtensionMethodModel>();
        var receiverParameterModel = new MethodParameterModel(
            Type: type,
            Name: receiverName,
            IsExtensionReceiver: true
        );

        methodModels.Add(CreateMethodModel(
            name: "Is",
            parameterModels: ImmutableArray.Create(
                receiverParameterModel,
                new MethodParameterModel(type, "value")
            ),
            body: $"return {receiverName} == value;",
            enumTypeModel
        ));

        methodModels.Add(CreateMethodModel(
            name: "IsNot",
            parameterModels: ImmutableArray.Create(
                receiverParameterModel,
                new MethodParameterModel(type, "value")
            ),
            body: $"return {receiverName} != value;",
            enumTypeModel
        ));

        foreach (var member in members)
        {
            methodModels.Add(CreateMethodModel(
                name: $"Is{member.Name}",
                parameterModels: ImmutableArray.Create(receiverParameterModel),
                body: $"return {receiverName} == {type}.{member.Name};",
                enumTypeModel
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

                methodModels.Add(CreateMethodModel(
                    name: $"Is{negatedMember}",
                    parameterModels: ImmutableArray.Create(receiverParameterModel),
                    body: $"return {receiverName} != {type}.{member.Name};",
                    enumTypeModel
                ));
            }
        }

        if (hasFlags)
        {
            methodModels.Add(CreateMethodModel(
                name: "Has",
                parameterModels: ImmutableArray.Create(
                    receiverParameterModel,
                    new MethodParameterModel(type, "value")
                ),
                body: $"return ({receiverName} & value) == value;",
                enumTypeModel
            ));

            methodModels.Add(CreateMethodModel(
                name: "HasNot",
                parameterModels: ImmutableArray.Create(
                    receiverParameterModel,
                    new MethodParameterModel(type, "value")
                ),
                body: $"return ({receiverName} & value) != value;",
                enumTypeModel
            ));

            foreach (var member in members.Where(static member => !IsZero(member.Value)))
            {
                methodModels.Add(CreateMethodModel(
                    name: $"Has{member.Name}",
                    parameterModels: ImmutableArray.Create(receiverParameterModel),
                    body: $"return ({receiverName} & {type}.{member.Name}) == {type}.{member.Name};",
                    enumTypeModel
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

                    methodModels.Add(CreateMethodModel(
                        name: $"HasNot{member.Name}",
                        parameterModels: ImmutableArray.Create(receiverParameterModel),
                        body: $"return ({receiverName} & {type}.{member.Name}) != {type}.{member.Name};",
                        enumTypeModel
                    ));
                }
            }
        }

        return methodModels.ToImmutable();
    }

    private static ExtensionMethodModel CreateMethodModel(
        string name,
        ImmutableArray<MethodParameterModel> parameterModels,
        string body,
        EnumTypeModel enumTypeModel
    )
    {
        return new ExtensionMethodModel(
            Name: name,
            GenericParameters: enumTypeModel.GenericParameters,
            GenericParameterConstraints: enumTypeModel.GenericParameterConstraints,
            Parameters: parameterModels,
            Body: body
        );
    }

    private static string GetNegatedMemberName(string memberName)
    {
        return memberName.StartsWith("Not") ? memberName[3..] : $"Not{memberName}";
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
