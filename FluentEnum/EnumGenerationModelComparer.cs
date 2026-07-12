namespace Macaron.FluentEnum;

public sealed class EnumGenerationModelComparer : IEqualityComparer<EnumGenerationModel>
{
    public static EnumGenerationModelComparer Instance { get; } = new();

    private EnumGenerationModelComparer()
    {
    }

    public bool Equals(EnumGenerationModel? x, EnumGenerationModel? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        var stringComparer = StringComparer.Ordinal;

        return ExtensionClassModelComparer.Instance.Equals(x.ExtensionClass, y.ExtensionClass)
            && stringComparer.Equals(x.HintName, y.HintName)
            && stringComparer.Equals(x.ReceiverName, y.ReceiverName)
            && EnumTypeModelComparer.Instance.Equals(x.EnumType, y.EnumType);
    }

    public int GetHashCode(EnumGenerationModel obj)
    {
        var stringComparer = StringComparer.Ordinal;

        unchecked
        {
            var hashCode = ExtensionClassModelComparer.Instance.GetHashCode(obj.ExtensionClass);

            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(obj.HintName);
            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(obj.ReceiverName);
            hashCode = (hashCode * 397) ^ EnumTypeModelComparer.Instance.GetHashCode(obj.EnumType);

            return hashCode;
        }
    }
}
