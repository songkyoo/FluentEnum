namespace Macaron.FluentEnum;

internal sealed class ExtensionClassModelComparer : IEqualityComparer<ExtensionClassModel>
{
    public static readonly ExtensionClassModelComparer Instance = new();

    private ExtensionClassModelComparer()
    {
    }

    public bool Equals(ExtensionClassModel? x, ExtensionClassModel? y)
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

        return stringComparer.Equals(x.Namespace, y.Namespace)
            && stringComparer.Equals(x.ClassName, y.ClassName)
            && stringComparer.Equals(x.AccessModifier, y.AccessModifier);
    }

    public int GetHashCode(ExtensionClassModel obj)
    {
        var stringComparer = StringComparer.Ordinal;

        unchecked
        {
            var hashCode = stringComparer.GetHashCode(obj.Namespace);

            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(obj.ClassName);
            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(obj.AccessModifier);

            return hashCode;
        }
    }
}
