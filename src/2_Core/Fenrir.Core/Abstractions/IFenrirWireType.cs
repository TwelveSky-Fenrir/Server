namespace Fenrir.Core.Abstractions;

public interface IFenrirWireType<TSelf>
    where TSelf : struct, IFenrirWireType<TSelf>
{
    public static abstract int WireSize { get; }

    public static abstract bool TryRead(ReadOnlySpan<byte> source, out TSelf value);

    public int Write(Span<byte> destination);
}
