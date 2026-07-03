namespace Fenrir.Contracts.Attributes;

/// <summary>
///     Marks N padding bytes BEFORE the field carrying this attribute (e.g. the 3 bytes between
///     <c>aName</c> and <c>aTribe</c> in AVATAR_INFO). Read and discarded; written as zero.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ReservedAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
