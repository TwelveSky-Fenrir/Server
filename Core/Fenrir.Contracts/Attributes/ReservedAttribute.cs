namespace Fenrir.Contracts.Attributes;

/// <summary>N padding bytes BEFORE this field (e.g. 3 bytes between <c>aName</c>/<c>aTribe</c> in AVATAR_INFO); read and discarded, written as zero.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ReservedAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
