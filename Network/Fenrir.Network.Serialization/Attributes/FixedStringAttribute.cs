namespace Fenrir.Network.Serialization.Attributes;

/// <summary>Fixed-width <c>char[N]</c> null-terminated, zero-padded, Latin-1 encoded.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FixedStringAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
