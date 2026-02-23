using System.Buffers;
using System.Runtime.InteropServices;
using Fenrir.Framework.Extensions;
using Fenrir.LoginServer.Handlers;
using Fenrir.LoginServer.Network.Metadata;
using Fenrir.Network.Collections;
using Fenrir.Network.Framing;

namespace Fenrir.LoginServer.Network.Framing;

public sealed class MessageDecoder  //: IMessageDecoder
{
    // public IEnumerable<MessageMetadata> DecodeMessages(ReadOnlySequence<byte> sequence, bool isReadByServer)
    // {
    //     var reader = new BinaryReader(new MemoryStream(sequence.ToArray()));
    //
    //     while (reader.BaseStream.Remaining() >= MessageMetadata.ByteSize)
    //     {
    //         if (!reader.BaseStream.TryRead(() => reader.ReadInt32(), out var messageLength))
    //             yield break;
    //
    //         if (!reader.BaseStream.TryRead(() => reader.ReadInt32(), out var messageUserId))
    //             yield break;
    //
    //         if (!reader.BaseStream.TryRead(() => reader.ReadByte(), out var messageProtocolId))
    //             yield break;
    //
    //         var messagePayload = reader.ReadBytes(reader.BaseStream.Remaining());
    //
    //         yield return new MessageMetadata(messageLength, messageUserId, messageProtocolId, messagePayload);
    //     }
    // }

    public IPacket TryReadPacket(ReadOnlySpan<byte> buffer, PacketCollection packetCollection)
    {
        //var packets = new List<LoginHandler.IPacket>();
        //var span = buffer.Span;
        int remainingBytes = buffer.Length;

        // Ensure there is at least 9 bytes in the buffer.
        if (remainingBytes < 9)
            return null;
            //return (packets, remainingBytes);

            // Decode your packet here
            
            // Read a PacketId byte from the buffer.
            var packetId = buffer[0];
        
        // Can packetId be converted into PacketType?
        if (!Enum.IsDefined(typeof(PacketType), packetId))
        {
            // Invalid packetId
            // Move the span forward and update remainingBytes
            //buffer = buffer.Slice(1);
            //remainingBytes -= 1;
            
            // TODO: Throw error unexpected packet?
            throw new Exception("Unexpected packet type.");
        }
        
        IPacket packet;
        
        var packetHeader = Marshaling.DeserializeStruct<PacketHeader>(buffer);
        buffer = buffer.Slice(9); // TODO: Rather than magic 9, could use Marshal.SizeOf<PacketHeader>() but if its a runtime method rather only call it once?
        
        // TODO: handle exceptions.
        // TODO: Consider maybe this should not throw and should instead return null for speed as entering TryCatch adds some overhead?
        var packetInfo = packetCollection.GetPacketInfo(packetHeader.PacketType);
        if (packetInfo == null)
        {
            // TODO: Custom exception so we can add type.
            throw new Exception("Unexpected packet type or packet not registered.");
        }
        // packetInfo.PacketType
        // Marshaling.DeserializeStructFromSpan<>(buffer);
        
        
        switch ((PacketType)packetHeader.PacketType) {
            case PacketType.LoginRequest:
                var LoginPacketData = Marshaling.DeserializeStruct<LoginHandler.LoginRequestPacket>(buffer);
                packet = LoginPacketData;
                buffer = buffer.Slice(Marshal.SizeOf<LoginHandler.LoginRequestPacket>());
                // TODO: Call handler here?
                // LoginHandler.LoginRequestHandler.Handle(null, LoginPacketData);
                break;
            
            default:
                throw new Exception("Unexpected packet type.");
        }

        return packet;
    }
    
    private static Func<ReadOnlyMemory<byte>, object> GetDeserializer(Type type)
    {
        // if (!_deserializers.TryGetValue(type, out var deserializer))
        // {
            var method = typeof(Marshaling).GetMethod(nameof(Marshaling.DeserializeStruct), [type]);
            var genericMethod = method.MakeGenericMethod(type);
            var deserializer = (Func<ReadOnlyMemory<byte>, object>)Delegate.CreateDelegate(typeof(Func<ReadOnlyMemory<byte>, object>), genericMethod);
        //     _deserializers[type] = deserializer;
        // }
        return deserializer;
    }
}