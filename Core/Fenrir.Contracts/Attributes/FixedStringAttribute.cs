namespace Fenrir.Contracts.Attributes;

/// <summary>
///     <c>string</c> field representing a fixed-width C++ <c>char[N]</c> (§0.3): null-terminated,
///     remaining bytes zero-padded, Windows-1252/Latin-1 encoding (<see cref="System.Text.Encoding.Latin1" />).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FixedStringAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
