namespace Fenrir.Network.Serialization.Attributes;

/// <summary>Fixed-width C++ <c>char[N]</c> (§0.3): null-terminated, zero-padded, Latin-1 encoded.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FixedStringAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
