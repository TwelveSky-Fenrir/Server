namespace Fenrir.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ReservedAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
