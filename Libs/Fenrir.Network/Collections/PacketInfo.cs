using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using Fenrir.Network.Framing;
using Fenrir.Network.Helpers;

namespace Fenrir.Network.Collections;

public class PacketInfo : IPacketInfo
{
    public byte Id { get; }
    public Type? PacketType { get; }
    public String Name { get; }
    public uint Size { get; }
    public bool IsCompressible { get; }
    
    public Func<ReadOnlySequence<byte>, object>? Deserialize { get; set; }

    public PacketInfo(byte id)
    {
        Id = id;
        PacketType = null;
        Name = id.ToString("X");
        Size = 0;
        IsCompressible = false;
    }

    public PacketInfo(Type packetType)
    {
        if (!typeof(IPacket).IsAssignableFrom(packetType))
        {
            throw new ArgumentException("Packet Type must implement IPacket.");
        }

        var attribute = packetType.GetCustomAttribute<PacketAttribute>();
        if (attribute == null)
        {
            throw new ArgumentException("Packet Type must have a PacketAttribute.");
        }

        if (attribute.PacketId == null)
        {
            throw new ArgumentException("PacketAttribute must have a PacketId.");
        }
        // System.InvalidCastException: Unable to cast object of type 'Fenrir.LoginServer.Network.Metadata.PacketType' to type 'System.Nullable`1[System.Byte]'.
        
        Id = (byte)attribute.PacketId;
        PacketType = packetType;

        Name = packetType.Name;
        Size = (uint)Marshal.SizeOf(packetType);

        IsCompressible = attribute.IsCompressible;
        
        
        var method = typeof(Marshaling).GetMethod(nameof(Marshaling.DeserializeStruct), new[] { typeof(ReadOnlySequence<byte>) });
        var genericMethod = method.MakeGenericMethod(packetType);
        //Deserialize = (Func<ReadOnlyMemory<byte>, object>)Delegate.CreateDelegate(typeof(Func<ReadOnlyMemory<byte>, object>), genericMethod);
        Deserialize = (Func<ReadOnlySequence<byte>, object>)Delegate.CreateDelegate(typeof(Func<ReadOnlySequence<byte>, object>), genericMethod);
    }

    //public Func<ReadOnlyMemory<byte>, object> Deserialize { get; set; }
    

    public PacketInfo(byte id, Type? packetType)
    {
        Id = id;
        PacketType = packetType;

        if (packetType != null)
        {
            if (!typeof(IPacket).IsAssignableFrom(packetType))
            {
                throw new ArgumentException("Packet Type must implement IPacket.");
            }

            Name = packetType.Name;
            Size = (uint)Marshal.SizeOf(packetType);
        }
        else
        {
            Name = id.ToString("X");
            Size = 0;
        }

        IsCompressible = false;
    }
    
}