namespace Fenrir.Network.Collections;

public interface IPacketInfo
{
    byte Id { get; }
    Type? PacketType { get; }
    String Name { get; }
    uint Size { get; }
    bool IsCompressible { get; }
}