using System.Collections.Immutable;

namespace Macaron.FluentEnum;

internal static class ImmutableArrayComparer
{
    public static bool Equals<T>(ImmutableArray<T> x, ImmutableArray<T> y, IEqualityComparer<T> itemComparer)
    {
        if (x.IsDefault || y.IsDefault)
        {
            return x.IsDefault == y.IsDefault;
        }

        if (x.Length != y.Length)
        {
            return false;
        }

        for (var i = 0; i < x.Length; i++)
        {
            if (!itemComparer.Equals(x[i], y[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static int GetHashCode<T>(ImmutableArray<T> values, IEqualityComparer<T> itemComparer)
    {
        if (values.IsDefault)
        {
            return 0;
        }

        unchecked
        {
            var hashCode = 17;

            foreach (var value in values)
            {
                hashCode = (hashCode * 31) + (value is null ? 0 : itemComparer.GetHashCode(value));
            }

            return hashCode;
        }
    }
}
