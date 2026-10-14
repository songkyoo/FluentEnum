using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal static class ExtensionMethodModelFactory
{
    public static ImmutableArray<ExtensionMethodModel> Create(
        EnumModel enumModel,
        CancellationToken cancellationToken
    )
    {
        var (generationModel, members, generateNegatedMembers, isFlags) = enumModel;
        var enumTypeModel = generationModel.EnumType;
        var type = enumTypeModel.Type;
        var receiverName = generationModel.ReceiverName;
        var memberSet = ImmutableHashSet.CreateBuilder<string>();

        foreach (var member in members)
        {
            memberSet.Add(member.Name);
        }

        cancellationToken.ThrowIfCancellationRequested();

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

        for (var count = 2; count <= 8; count++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddIsSetMethods(methodModels, receiverParameterModel, receiverName, type, enumTypeModel, count);
        }

        AddIsSetMethods(methodModels, receiverParameterModel, receiverName, type, enumTypeModel, 8, hasParams: true);

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                cancellationToken.ThrowIfCancellationRequested();

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

        if (isFlags)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            methodModels.Add(CreateMethodModel(
                name: "HasAny",
                parameterModels: ImmutableArray.Create(
                    receiverParameterModel,
                    new MethodParameterModel(type, "value")
                ),
                body: $"return ({receiverName} & value) != 0;",
                enumTypeModel
            ));

            methodModels.Add(CreateMethodModel(
                name: "HasNone",
                parameterModels: ImmutableArray.Create(
                    receiverParameterModel,
                    new MethodParameterModel(type, "value")
                ),
                body: $"return ({receiverName} & value) == 0;",
                enumTypeModel
            ));

            methodModels.Add(CreateMethodModel(
                name: "Add",
                parameterModels: ImmutableArray.Create(
                    receiverParameterModel,
                    new MethodParameterModel(type, "value")
                ),
                body: $"return {receiverName} | value;",
                enumTypeModel,
                returnType: type
            ));

            methodModels.Add(CreateMethodModel(
                name: "Remove",
                parameterModels: ImmutableArray.Create(
                    receiverParameterModel,
                    new MethodParameterModel(type, "value")
                ),
                body: $"return {receiverName} & ~value;",
                enumTypeModel,
                returnType: type
            ));

            foreach (var member in members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsZero(member.Value))
                {
                    continue;
                }

                methodModels.Add(CreateMethodModel(
                    name: $"Has{member.Name}",
                    parameterModels: ImmutableArray.Create(receiverParameterModel),
                    body: $"return ({receiverName} & {type}.{member.Name}) == {type}.{member.Name};",
                    enumTypeModel
                ));
            }

            if (generateNegatedMembers)
            {
                foreach (var member in members)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsZero(member.Value))
                    {
                        continue;
                    }

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
        EnumTypeModel enumTypeModel,
        string returnType = "bool"
    )
    {
        return new ExtensionMethodModel(
            Name: name,
            ReturnType: returnType,
            GenericParameters: enumTypeModel.GenericParameters,
            GenericParameterConstraints: enumTypeModel.GenericParameterConstraints,
            Parameters: parameterModels,
            Body: body
        );
    }

    private static void AddIsSetMethods(
        ImmutableArray<ExtensionMethodModel>.Builder methodModels,
        MethodParameterModel receiverParameterModel,
        string receiverName,
        string type,
        EnumTypeModel enumTypeModel,
        int count,
        bool hasParams = false
    )
    {
        var parameters = ImmutableArray.CreateBuilder<MethodParameterModel>();
        parameters.Add(receiverParameterModel);

        for (var i = 1; i <= count; i++)
        {
            parameters.Add(new MethodParameterModel(type, $"value{i}"));
        }

        if (hasParams)
        {
            parameters.Add(new MethodParameterModel($"{type}[]", "values", IsParams: true));
        }

        var comparisons = string.Join(" || ", Enumerable.Range(1, count).Select(i => $"{receiverName} == value{i}"));
        var anyBody = $"return {comparisons};";
        var noneBody = $"return !({comparisons});";

        if (hasParams)
        {
            anyBody = $"if ({comparisons}) return true;\nforeach (var value in values)\n{{\n    if ({receiverName} == value) return true;\n}}\nreturn false;";
            noneBody = $"if ({comparisons}) return false;\nforeach (var value in values)\n{{\n    if ({receiverName} == value) return false;\n}}\nreturn true;";
        }

        methodModels.Add(CreateMethodModel("IsAny", parameters.ToImmutable(), anyBody, enumTypeModel));
        methodModels.Add(CreateMethodModel("IsNone", parameters.ToImmutable(), noneBody, enumTypeModel));
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
