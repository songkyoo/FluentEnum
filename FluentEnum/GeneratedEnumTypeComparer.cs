namespace Macaron.FluentEnum;

internal sealed class GeneratedEnumTypeComparer : IEqualityComparer<GeneratedEnumType>
{
    public static readonly GeneratedEnumTypeComparer Instance = new();

    private GeneratedEnumTypeComparer()
    {
    }

    public bool Equals(GeneratedEnumType x, GeneratedEnumType y)
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

    public int GetHashCode(GeneratedEnumType obj)
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
