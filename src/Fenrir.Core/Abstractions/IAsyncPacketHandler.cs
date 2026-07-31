namespace Fenrir.Core.Abstractions;

public interface IAsyncPacketHandler<in TPacket>
    where TPacket : struct, IIncomingPacket<TPacket>
{
    public ValueTask HandleAsync(TPacket packet, IPacketSession session, CancellationToken cancellationToken);
}
