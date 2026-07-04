namespace Fenrir.Contracts.Abstractions;

/// <summary>Embeddable wire sub-struct with no opcode (e.g. AVATAR_INFO); never travels alone on the wire.</summary>
public interface IFenrirWireType<TSelf>
    where TSelf : struct, IFenrirWireType<TSelf>
{
    public static abstract int WireSize { get; }

    public static abstract bool TryRead(ReadOnlySpan<byte> source, out TSelf value);

    public int Write(Span<byte> destination);
}
