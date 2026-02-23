using Fenrir.LoginServer.Network.Metadata;

namespace Fenrir.LoginServer.Network.Protocol;

// public class LoginRequestPacket : Packet
// {
//     // TODO: Implement PacketType enum
//
//     private new static readonly PacketType Type = PacketType.LoginRequest;
//     public override PacketType PacketType => Type;
//
//     public string Username { get; set; }
//     public string Password { get; set; }
//
//     public override void Deserialize(BinaryReader reader)
//     {
//         Username = reader.ReadString();
//         Password = reader.ReadString();
//     }
//
//     public override void Serialize(BinaryWriter writer)
//     {
//         writer.Write(Username);
//         writer.Write(Password);
//     }
// }