namespace Fenrir.Network.Collections;

public class PacketCollection : IPacketCollection
{
    private readonly Dictionary<byte, PacketInfo> _packets = new();

    public void RegisterPacket(byte id)
    {
        if (!_packets.TryAdd(id, new PacketInfo(id)))
        {
            throw new ArgumentException($"Packet with ID {id} already exists.");
        }
    }
    
    public void RegisterPacket(PacketInfo packetInfo)
    {
        if (!_packets.TryAdd(packetInfo.Id, packetInfo))
        {
            throw new ArgumentException($"Packet with ID {packetInfo.Id} already exists.");
        }
    }
  
    public PacketInfo GetPacketInfo(byte id)
    {
        if (_packets.TryGetValue(id, out var packetInfo))
        {
            return packetInfo;
        }
        throw new KeyNotFoundException($"Packet with ID {id} not found.");
    }

    public void ForEach(Action<PacketInfo> action)
    {
        foreach (var packet in _packets.Values)
        {
            action(packet);
        }
    }
}
