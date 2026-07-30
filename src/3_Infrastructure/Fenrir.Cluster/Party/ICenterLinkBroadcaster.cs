namespace Fenrir.Cluster.Party;

public interface ICenterLinkBroadcaster
{
    public void BroadcastToZones<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}
