namespace Macaron.FluentEnum;

internal sealed class EnumContextComparer : IEqualityComparer<EnumContext>
{
    public static readonly EnumContextComparer Instance = new();

    private EnumContextComparer()
    {
    }

    public bool Equals(EnumContext? x, EnumContext? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return EnumTypeContextComparer.Instance.Equals(x.TypeContext, y.TypeContext)
            && ImmutableArrayComparer.Equals(
                x.Members,
                y.Members,
                EqualityComparer<EnumMember>.Default
            )
            && x.GenerateNegatedMembers == y.GenerateNegatedMembers
            && x.HasFlags == y.HasFlags;
    }

    public int GetHashCode(EnumContext obj)
    {
        unchecked
        {
            var hashCode = EnumTypeContextComparer.Instance.GetHashCode(obj.TypeContext);

            hashCode = (hashCode * 397) ^ ImmutableArrayComparer.GetHashCode(
                obj.Members,
                EqualityComparer<EnumMember>.Default
            );

            hashCode = (hashCode * 397) ^ obj.GenerateNegatedMembers.GetHashCode();
            hashCode = (hashCode * 397) ^ obj.HasFlags.GetHashCode();

            return hashCode;
        }
    }
}
