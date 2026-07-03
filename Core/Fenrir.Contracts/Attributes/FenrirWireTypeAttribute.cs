namespace Fenrir.Contracts.Attributes;

/// <summary>
///     Marks a <c>readonly partial record struct</c> as an embeddable wire sub-struct
///     (AVATAR_INFO, ACTION_INFO, WORLD_INFO, ...): no opcode of its own, never sent alone.
///     The generator emits <c>TryRead</c>/<c>Write</c>/<c>WireSize</c> (<see cref="Abstractions.IFenrirWireType{TSelf}" />
///     ).
/// </summary>
/// <param name="expectedSize">Size copied from the wire contract's <c>sizeof</c> column; -1 = unchecked.</param>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class FenrirWireTypeAttribute(int expectedSize = -1) : Attribute
{
    public int ExpectedSize { get; } = expectedSize;
}
