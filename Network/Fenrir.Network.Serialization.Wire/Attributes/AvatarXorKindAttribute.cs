namespace Fenrir.Network.Serialization.Wire.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AvatarXorKindAttribute(AvatarXorKind kind, int rowLength = 0) : Attribute
{
    public AvatarXorKind Kind { get; } = kind;

    public int RowLength { get; } = rowLength;
}
