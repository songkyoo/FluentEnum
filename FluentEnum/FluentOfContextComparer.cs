namespace Macaron.FluentEnum;

internal sealed class FluentOfContextComparer : IEqualityComparer<FluentOfContext>
{
    public static readonly FluentOfContextComparer Instance = new();

    private FluentOfContextComparer()
    {
    }

    public bool Equals(FluentOfContext? x, FluentOfContext? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return ExtensionClassContextComparer.Instance.Equals(x.ExtensionClassContext, y.ExtensionClassContext)
            && EnumContextComparer.Instance.Equals(x.EnumContext, y.EnumContext);
    }

    public int GetHashCode(FluentOfContext obj)
    {
        unchecked
        {
            var hashCode = ExtensionClassContextComparer.Instance.GetHashCode(obj.ExtensionClassContext);

            hashCode = (hashCode * 397) ^ EnumContextComparer.Instance.GetHashCode(obj.EnumContext);

            return hashCode;
        }
    }
}
