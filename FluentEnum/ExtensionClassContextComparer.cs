namespace Macaron.FluentEnum;

internal sealed class ExtensionClassContextComparer : IEqualityComparer<ExtensionClassContext>
{
    public static readonly ExtensionClassContextComparer Instance = new();

    private ExtensionClassContextComparer()
    {
    }

    public bool Equals(ExtensionClassContext? x, ExtensionClassContext? y)
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

    public int GetHashCode(ExtensionClassContext obj)
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
