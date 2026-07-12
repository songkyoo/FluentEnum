namespace Macaron.FluentEnum;

public sealed class FluentOfModelComparer : IEqualityComparer<FluentOfModel>
{
    public static readonly FluentOfModelComparer Instance = new();

    private FluentOfModelComparer()
    {
    }

    public bool Equals(FluentOfModel? x, FluentOfModel? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return ExtensionClassModelComparer.Instance.Equals(x.ExtensionClass, y.ExtensionClass)
            && EnumModelComparer.Instance.Equals(x.Enum, y.Enum);
    }

    public int GetHashCode(FluentOfModel obj)
    {
        unchecked
        {
            var hashCode = ExtensionClassModelComparer.Instance.GetHashCode(obj.ExtensionClass);

            hashCode = (hashCode * 397) ^ EnumModelComparer.Instance.GetHashCode(obj.Enum);

            return hashCode;
        }
    }
}
