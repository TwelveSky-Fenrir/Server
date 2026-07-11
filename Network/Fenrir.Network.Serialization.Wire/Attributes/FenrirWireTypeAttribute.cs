namespace Fenrir.Network.Serialization.Wire.Attributes;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class FenrirWireTypeAttribute(int expectedSize = -1) : Attribute
{
    public int ExpectedSize { get; } = expectedSize;
}
