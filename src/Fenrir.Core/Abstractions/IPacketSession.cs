namespace Fenrir.Core.Abstractions;

public interface IPacketSession
{
    public long SessionId { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}
