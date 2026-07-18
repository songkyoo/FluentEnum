namespace Macaron.FluentEnum;

public sealed class EnumTypeModelComparer : IEqualityComparer<EnumTypeModel>
{
    public static readonly EnumTypeModelComparer Instance = new();

    private EnumTypeModelComparer()
    {
    }

    public bool Equals(EnumTypeModel x, EnumTypeModel y)
    {
        var stringComparer = StringComparer.Ordinal;

        return stringComparer.Equals(x.Type, y.Type)
            && stringComparer.Equals(x.GenericParameters, y.GenericParameters)
            && ImmutableArrayComparer.Equals(
                x.GenericParameterConstraints,
                y.GenericParameterConstraints,
                stringComparer
            );
    }

    public int GetHashCode(EnumTypeModel obj)
    {
        var stringComparer = StringComparer.Ordinal;

        unchecked
        {
            var hashCode = stringComparer.GetHashCode(obj.Type);

            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(obj.GenericParameters);
            hashCode = (hashCode * 397) ^ ImmutableArrayComparer.GetHashCode(
                obj.GenericParameterConstraints,
                stringComparer
            );

            return hashCode;
        }
    }
}
