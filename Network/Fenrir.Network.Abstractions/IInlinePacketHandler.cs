namespace Fenrir.Network.Abstractions;

public interface IInlinePacketHandler<TPacket>
    where TPacket : struct, IIncomingPacket<TPacket>
{
    public void Handle(in TPacket packet, IPacketSession session);
}
