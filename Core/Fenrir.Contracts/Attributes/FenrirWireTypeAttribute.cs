namespace Fenrir.Contracts.Attributes;

/// <summary>Marks an embeddable wire sub-struct (AVATAR_INFO, ACTION_INFO, ...): no opcode, never sent alone.</summary>
/// <param name="expectedSize">Size copied from the wire contract's <c>sizeof</c> column; -1 = unchecked.</param>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class FenrirWireTypeAttribute(int expectedSize = -1) : Attribute
{
    public int ExpectedSize { get; } = expectedSize;
}
