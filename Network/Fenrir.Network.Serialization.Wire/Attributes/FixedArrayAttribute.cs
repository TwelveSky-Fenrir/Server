namespace Fenrir.Network.Serialization.Wire.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class FixedArrayAttribute(int elementCount) : Attribute
{
    public int ElementCount { get; } = elementCount;
}
