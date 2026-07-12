namespace Macaron.FluentEnum;

public sealed class EnumModelComparer : IEqualityComparer<EnumModel>
{
    public static readonly EnumModelComparer Instance = new();

    private EnumModelComparer()
    {
    }

    public bool Equals(EnumModel? x, EnumModel? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return EnumGenerationModelComparer.Instance.Equals(x.Generation, y.Generation)
            && ImmutableArrayComparer.Equals(
                x.Members,
                y.Members,
                EqualityComparer<EnumMember>.Default
            )
            && x.GenerateNegatedMembers == y.GenerateNegatedMembers
            && x.HasFlags == y.HasFlags;
    }

    public int GetHashCode(EnumModel obj)
    {
        unchecked
        {
            var hashCode = EnumGenerationModelComparer.Instance.GetHashCode(obj.Generation);

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
