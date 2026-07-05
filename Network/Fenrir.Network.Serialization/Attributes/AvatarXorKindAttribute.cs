namespace Fenrir.Network.Serialization.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AvatarXorKindAttribute(AvatarXorKind kind, int rowLength = 0) : Attribute
{
    public AvatarXorKind Kind { get; } = kind;

    /// <summary>Row width in bytes — only for <see cref="AvatarXorKind.Char2" /> (e.g. 13 for <c>Friend</c>).</summary>
    public int RowLength { get; } = rowLength;
}
