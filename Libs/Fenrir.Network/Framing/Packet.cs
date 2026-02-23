using System.Runtime.InteropServices;

namespace Fenrir.Network.Framing;

public interface IPacketHeader : IPacket
{
    public byte PacketType { get; set; }
    public uint Unknown1 { get; set; }
    public uint Unknown2 { get; set; }
        
    void DoSomething()
    {
            
    }
}
    
public interface IPacket
{
    void DoSomething()
    {
            
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketHeader : IPacketHeader
{
    public byte PacketType { get; set; }
    public uint Unknown1 { get; set; }
    public uint Unknown2 { get; set; }  // I feel like this was a short and two bytes?

    public void DoSomething()
    {
            
    }
}