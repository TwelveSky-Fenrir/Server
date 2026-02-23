namespace Fenrir.LoginServer.Network.Metadata;

public abstract class Packet : Message<PacketType>
{
    public override PacketType PacketType => Type;

    // IsCompressed byte.
    // protected bool IsCompressed => false; // TODO: Should this be in metadata?
    // Compressed Size, Decompressed Size
    // CompressedData ????
}