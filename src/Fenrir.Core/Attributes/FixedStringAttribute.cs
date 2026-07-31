namespace Fenrir.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class FixedStringAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
