namespace Fenrir.Contracts.Abstractions;

/// <summary>
///     Client → server packet. <c>source</c> starts after the <c>CLIENT_PACKET</c> header (already consumed by the
///     dispatcher).
/// </summary>
public interface IIncomingPacket<TSelf> : IFenrirPacket
    where TSelf : struct, IIncomingPacket<TSelf>
{
    public static abstract bool TryRead(ReadOnlySpan<byte> source, out TSelf packet);
}
