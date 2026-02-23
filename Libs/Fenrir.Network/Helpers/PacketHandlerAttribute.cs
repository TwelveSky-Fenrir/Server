using System.Reflection;
using Fenrir.Network.Framing;

namespace Fenrir.Network.Helpers;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PacketHandlerAttribute : Attribute
{
    public object? PacketId { get; }

    public PacketHandlerAttribute()
    {
    }

    public PacketHandlerAttribute(object packetId)
    {
        PacketId = packetId;
    }
    
    public PacketHandlerAttribute(Type packetType)
    {
        if (!typeof(IPacket).IsAssignableFrom(packetType))
        {
            throw new ArgumentException("Packet type must implement IPacket");
        }
        
        // Get PacketAttribute from packetType
        var packetAttribute = packetType.GetCustomAttribute<PacketAttribute>();
        if (packetAttribute == null)
        {
            throw new ArgumentException("Packet type must have a PacketAttribute");
        }

        PacketId = packetAttribute.PacketId ?? throw new ArgumentException("PacketAttribute must have a PacketId");
    }

}