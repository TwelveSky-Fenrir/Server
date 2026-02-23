using Fenrir.LoginServer.Network.Metadata;

namespace Fenrir.LoginServer.Network.Protocol;

public sealed class ServerZoneInfoRequestMessage : Packet
{
    public new const PacketType Type = PacketType.ServerZoneInfo;

    public ServerZoneInfoRequestMessage()
    {
    }

    public ServerZoneInfoRequestMessage(int avatarPost)
    {
        AvatarPost = avatarPost;
    }

    public override PacketType PacketType => Type;

    public int AvatarPost { get; set; }


    public override void Deserialize(BinaryReader reader)
    {
        AvatarPost = reader.ReadInt32();
    }

    public override void Serialize(BinaryWriter writer)
    {
        writer.Write(AvatarPost);
    }
}