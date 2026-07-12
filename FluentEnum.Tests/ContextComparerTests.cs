using System.Collections.Immutable;

namespace Macaron.FluentEnum.Tests;

[TestFixture]
public class ContextComparerTests
{
    [Test]
    public void GeneratedEnumTypeComparer_Should_CompareConstraintValues()
    {
        var x = new GeneratedEnumType(
            Type: "global::Foo<T>",
            GenericParameters: "<T>",
            GenericParameterConstraints: ImmutableArray.Create("where T : class")
        );
        var y = new GeneratedEnumType(
            Type: "global::Foo<T>",
            GenericParameters: "<T>",
            GenericParameterConstraints: ImmutableArray.Create("where T : class")
        );
        var different = y with
        {
            GenericParameterConstraints = ImmutableArray.Create("where T : struct"),
        };

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedEnumTypeComparer.Instance.Equals(x, y), Is.True);
            Assert.That(
                GeneratedEnumTypeComparer.Instance.GetHashCode(x),
                Is.EqualTo(GeneratedEnumTypeComparer.Instance.GetHashCode(y))
            );
            Assert.That(GeneratedEnumTypeComparer.Instance.Equals(x, different), Is.False);
        });
    }

    [Test]
    public void EnumTypeContextComparer_Should_CompareGeneratedValues()
    {
        var generatedType = new GeneratedEnumType(
            Type: "global::Foo",
            GenericParameters: "",
            GenericParameterConstraints: ImmutableArray<string>.Empty
        );
        var x = new EnumTypeContext(
            Namespace: "Example",
            ExtensionClassName: "FooExtensions",
            AccessModifier: "public",
            HintName: "Foo_0.00000000.g.cs",
            ReceiverName: "foo",
            GeneratedType: generatedType
        );
        var y = x with
        {
            GeneratedType = generatedType with
            {
                GenericParameterConstraints = ImmutableArray<string>.Empty,
            },
        };

        Assert.Multiple(() =>
        {
            Assert.That(EnumTypeContextComparer.Instance.Equals(x, y), Is.True);
            Assert.That(
                EnumTypeContextComparer.Instance.GetHashCode(x),
                Is.EqualTo(EnumTypeContextComparer.Instance.GetHashCode(y))
            );
            Assert.That(
                EnumTypeContextComparer.Instance.Equals(x, y with { ReceiverName = "value" }),
                Is.False
            );
        });
    }

    [Test]
    public void EnumContextComparer_Should_CompareMemberValues()
    {
        var typeContext = new EnumTypeContext(
            Namespace: "Example",
            ExtensionClassName: "FooExtensions",
            AccessModifier: "public",
            HintName: "Foo_0.00000000.g.cs",
            ReceiverName: "foo",
            GeneratedType: new GeneratedEnumType(
                Type: "global::Foo",
                GenericParameters: "",
                GenericParameterConstraints: ImmutableArray<string>.Empty
            )
        );
        var x = new EnumContext(
            TypeContext: typeContext,
            Members: ImmutableArray.Create(new EnumMember("None", 0)),
            GenerateNegatedMembers: true,
            HasFlags: false
        );
        var y = x with
        {
            Members = ImmutableArray.Create(new EnumMember("None", 0)),
        };

        Assert.Multiple(() =>
        {
            Assert.That(EnumContextComparer.Instance.Equals(x, y), Is.True);
            Assert.That(
                EnumContextComparer.Instance.GetHashCode(x),
                Is.EqualTo(EnumContextComparer.Instance.GetHashCode(y))
            );
            Assert.That(
                EnumContextComparer.Instance.Equals(
                    x,
                    y with { Members = ImmutableArray.Create(new EnumMember("Other", 1)) }
                ),
                Is.False
            );
            Assert.That(
                EnumContextComparer.Instance.Equals(x, y with { HasFlags = true }),
                Is.False
            );
        });
    }
}
