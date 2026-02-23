namespace Fenrir.Network.Collections;

public interface IPacketCollection
{
    void RegisterPacket(byte id);
    void RegisterPacket(PacketInfo packetInfo);
    PacketInfo GetPacketInfo(byte id);
    void ForEach(Action<PacketInfo> action);
}