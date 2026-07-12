namespace Macaron.FluentEnum;

internal sealed class EnumTypeContextComparer : IEqualityComparer<EnumTypeContext>
{
    public static EnumTypeContextComparer Instance { get; } = new();

    private EnumTypeContextComparer()
    {
    }

    public bool Equals(EnumTypeContext? x, EnumTypeContext? y)
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

        return ExtensionClassContextComparer.Instance.Equals(x.ExtensionClassContext, y.ExtensionClassContext)
            && stringComparer.Equals(x.HintName, y.HintName)
            && stringComparer.Equals(x.ReceiverName, y.ReceiverName)
            && GeneratedEnumTypeComparer.Instance.Equals(x.GeneratedType, y.GeneratedType);
    }

    public int GetHashCode(EnumTypeContext obj)
    {
        var stringComparer = StringComparer.Ordinal;

        unchecked
        {
            var hashCode = ExtensionClassContextComparer.Instance.GetHashCode(obj.ExtensionClassContext);

            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(obj.HintName);
            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(obj.ReceiverName);
            hashCode = (hashCode * 397) ^ GeneratedEnumTypeComparer.Instance.GetHashCode(obj.GeneratedType);

            return hashCode;
        }
    }
}
