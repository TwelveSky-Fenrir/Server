namespace Fenrir.Network.Framing;

// TODO: Is this specific per server?
public struct MessageHeader
{
    private byte PacketType;
    private int PacketLength;
    private int Counter;
    
    // Compressed?
    // Encrypted?
}