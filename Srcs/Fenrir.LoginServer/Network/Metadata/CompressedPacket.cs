// namespace Fenrir.LoginServer.Network.Metadata;
//
// public class CompressedPacket : Packet
// {
//     protected bool IsCompressed => true;
//     
//     uint CompressedSize { get; set; }
//     uint DecompressedSize { get; set; }
//     byte[] CompressedData { get; set; }
//     byte[] DecompressedData { get; set; }
//
//     public override void Deserialize(BinaryReader reader)
//     {
//         throw new NotImplementedException();
//     }
//
//     public override void Serialize(BinaryWriter writer)
//     {
//         throw new NotImplementedException();
//     }
// }