namespace Fenrir.Core.Abstractions;

public interface IIncomingPacket<TSelf> : IFenrirPacket
    where TSelf : struct, IIncomingPacket<TSelf>
{
    public static abstract bool TryRead(ReadOnlySpan<byte> source, out TSelf packet);
}
