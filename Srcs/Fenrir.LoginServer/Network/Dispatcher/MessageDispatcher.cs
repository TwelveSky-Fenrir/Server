using Fenrir.LoginServer.Network.Metadata;
using Fenrir.Network.Dispatcher;
using Fenrir.Network.Transport;

namespace Fenrir.LoginServer.Network.Dispatcher;

// TODO: Fix this or remove it?
// using SessionType = Session<PacketType, MessageMetadata, Packet>;
// using NetworkHandlerFunction = Func<Session<PacketType, MessageMetadata, Packet>, Packet, ValueTask>;
//
// // TODO: Find how to add Message Metadata, session data etc.
// // TODO: how to say what type we want to use for packet identifier?
// // TODO: Can I make IMessage have Dynamic Type of TPacketType?
// public class MessageDispatcher : IMessageDispatcher<PacketType, MessageMetadata, Packet>
// {
//     private readonly Dictionary<PacketType, NetworkHandlerFunction> _handlers =
//         new();
//
//     public async Task<DispatchResults> DispatchAsync(SessionType session, Packet message)
//     {
//         if (!_handlers.TryGetValue(message.PacketType, out var handler))
//             return DispatchResults.NotMapped;
//         await handler(session, message);
//         return DispatchResults.Succeeded;
//     }
//
//     public bool RegisterHandler(byte packetType, NetworkHandlerFunction handler)
//     {
//         return _handlers.TryAdd((PacketType)packetType, handler);
//     }
//
//     public bool RegisterHandler(PacketType packetType, NetworkHandlerFunction handler)
//     {
//         return _handlers.TryAdd(packetType, handler);
//     }
// }