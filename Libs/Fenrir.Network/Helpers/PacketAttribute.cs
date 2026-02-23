namespace Fenrir.Network.Helpers;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class PacketAttribute : Attribute
{
    public object? PacketId { get; }
    public bool IsCompressible { get; set; }

    public PacketAttribute() { }

    public PacketAttribute(object packetId)
    {
        PacketId = packetId ?? throw new ArgumentException("Packet Attribute must have a PacketId.");
    }
    
    public PacketAttribute(object packetId, bool isCompressible)
    {
        PacketId = packetId ?? throw new ArgumentException("Packet Attribute must have a PacketId.");
        IsCompressible = isCompressible;
    }
}