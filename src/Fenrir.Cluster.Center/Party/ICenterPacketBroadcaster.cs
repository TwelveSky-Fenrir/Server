namespace Fenrir.Cluster.Center.Party;

public interface ICenterPacketBroadcaster
{
    public void BroadcastToZones<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}
