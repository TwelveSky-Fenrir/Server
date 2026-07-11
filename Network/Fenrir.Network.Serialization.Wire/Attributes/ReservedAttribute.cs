namespace Fenrir.Network.Serialization.Wire.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ReservedAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
