namespace Fenrir.Contracts.Abstractions;

/// <summary>
///     Embeddable wire sub-struct with no opcode of its own (e.g. AVATAR_INFO, ACTION_INFO, WORLD_INFO); unlike
///     <see cref="IFenrirPacket" />, it never travels alone on the wire.
/// </summary>
public interface IFenrirWireType<TSelf>
    where TSelf : struct, IFenrirWireType<TSelf>
{
    public static abstract int WireSize { get; }

    public static abstract bool TryRead(ReadOnlySpan<byte> source, out TSelf value);

    public int Write(Span<byte> destination);
}
