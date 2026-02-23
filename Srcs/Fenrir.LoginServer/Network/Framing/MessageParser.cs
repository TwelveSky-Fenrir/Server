using System.Buffers;
using Fenrir.LoginServer.Network.Metadata;
using Fenrir.Network.Framing;

namespace Fenrir.LoginServer.Network.Framing;

// public class MessageParser : IMessageParser<MessageMetadata>
// {
//     private readonly IMessageDecoder<MessageMetadata> _decoder = new MessageDecoder();
//     private readonly IMessageEncoder<MessageMetadata> _encoder = new MessageEncoder();
//
//     public ReadOnlyMemory<byte> EncodeMessage(MessageMetadata message, bool isWrittenByServer)
//     {
//         return _encoder.EncodeMessage(message, isWrittenByServer);
//     }
//
//     public IEnumerable<MessageMetadata> DecodeMessages(ReadOnlySequence<byte> sequence, bool isReadByServer)
//     {
//         return _decoder.DecodeMessages(sequence, isReadByServer);
//     }
// }