using System.Collections.Immutable;

namespace Macaron.FluentEnum.Tests;

[TestFixture]
public class ModelComparerTests
{
    [Test]
    public void EnumTypeModelComparer_Should_CompareConstraintValues()
    {
        var x = new EnumTypeModel(
            Type: "global::Foo<T>",
            GenericParameters: "<T>",
            GenericParameterConstraints: ImmutableArray.Create("where T : class")
        );
        var y = new EnumTypeModel(
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
            Assert.That(EnumTypeModelComparer.Instance.Equals(x, y), Is.True);
            Assert.That(
                EnumTypeModelComparer.Instance.GetHashCode(x),
                Is.EqualTo(EnumTypeModelComparer.Instance.GetHashCode(y))
            );
            Assert.That(EnumTypeModelComparer.Instance.Equals(x, different), Is.False);
        });
    }

    [Test]
    public void EnumGenerationModelComparer_Should_CompareGeneratedValues()
    {
        var enumTypeModel = new EnumTypeModel(
            Type: "global::Foo",
            GenericParameters: "",
            GenericParameterConstraints: ImmutableArray<string>.Empty
        );
        var x = new EnumGenerationModel(
            ExtensionClass: new ExtensionClassModel(
                Namespace: "Example",
                ClassName: "FooExtensions",
                AccessModifier: "public"
            ),
            HintName: "Foo_0.00000000.g.cs",
            ReceiverName: "foo",
            EnumType: enumTypeModel
        );
        var y = x with
        {
            EnumType = enumTypeModel with
            {
                GenericParameterConstraints = ImmutableArray<string>.Empty,
            },
        };

        Assert.Multiple(() =>
        {
            Assert.That(EnumGenerationModelComparer.Instance.Equals(x, y), Is.True);
            Assert.That(
                EnumGenerationModelComparer.Instance.GetHashCode(x),
                Is.EqualTo(EnumGenerationModelComparer.Instance.GetHashCode(y))
            );
            Assert.That(
                EnumGenerationModelComparer.Instance.Equals(x, y with { ReceiverName = "value" }),
                Is.False
            );
        });
    }

    [Test]
    public void EnumModelComparer_Should_CompareMemberValues()
    {
        var generationModel = new EnumGenerationModel(
            ExtensionClass: new ExtensionClassModel(
                Namespace: "Example",
                ClassName: "FooExtensions",
                AccessModifier: "public"
            ),
            HintName: "Foo_0.00000000.g.cs",
            ReceiverName: "foo",
            EnumType: new EnumTypeModel(
                Type: "global::Foo",
                GenericParameters: "",
                GenericParameterConstraints: ImmutableArray<string>.Empty
            )
        );
        var x = new EnumModel(
            Generation: generationModel,
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
            Assert.That(EnumModelComparer.Instance.Equals(x, y), Is.True);
            Assert.That(
                EnumModelComparer.Instance.GetHashCode(x),
                Is.EqualTo(EnumModelComparer.Instance.GetHashCode(y))
            );
            Assert.That(
                EnumModelComparer.Instance.Equals(
                    x,
                    y with { Members = ImmutableArray.Create(new EnumMember("Other", 1)) }
                ),
                Is.False
            );
            Assert.That(
                EnumModelComparer.Instance.Equals(x, y with { HasFlags = true }),
                Is.False
            );
        });
    }

    [Test]
    public void FluentOfModelComparer_Should_CompareExtensionClassAndEnumValues()
    {
        var enumModel = new EnumModel(
            Generation: new EnumGenerationModel(
                ExtensionClass: new ExtensionClassModel(
                    Namespace: "Example",
                    ClassName: "FooExtensions",
                    AccessModifier: "public"
                ),
                HintName: "Foo_0.00000000.g.cs",
                ReceiverName: "foo",
                EnumType: new EnumTypeModel(
                    Type: "global::Example.Foo",
                    GenericParameters: "",
                    GenericParameterConstraints: ImmutableArray<string>.Empty
                )
            ),
            Members: ImmutableArray.Create(new EnumMember("None", 0)),
            GenerateNegatedMembers: true,
            HasFlags: false
        );
        var x = new FluentOfModel(
            ExtensionClass: new ExtensionClassModel(
                Namespace: "Example",
                ClassName: "CustomFooExtensions",
                AccessModifier: "public"
            ),
            Enum: enumModel
        );
        var y = x with
        {
            ExtensionClass = x.ExtensionClass with { Namespace = "Example" },
            Enum = enumModel with
            {
                Members = ImmutableArray.Create(new EnumMember("None", 0)),
            },
        };

        Assert.Multiple(() =>
        {
            Assert.That(FluentOfModelComparer.Instance.Equals(x, y), Is.True);
            Assert.That(
                FluentOfModelComparer.Instance.GetHashCode(x),
                Is.EqualTo(FluentOfModelComparer.Instance.GetHashCode(y))
            );
            Assert.That(
                FluentOfModelComparer.Instance.Equals(
                    x,
                    y with
                    {
                        ExtensionClass = y.ExtensionClass with
                        {
                            ClassName = "OtherExtensions",
                        },
                    }
                ),
                Is.False
            );
        });
    }
}
