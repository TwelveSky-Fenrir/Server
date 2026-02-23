using Fenrir.Network.Framing;
using Fenrir.Network.Transport;

namespace Fenrir.Network.Dispatcher;

/// <summary>Represents a dispatcher that can be used to invoke asynchronous message delegates.</summary>
// public interface IMessageDispatcher<TPacketType, TMessage>
// {
//     // What about ValueTask?
//     Task<DispatchResults> DispatchAsync(IClient client, TMessage message);
//
//     bool RegisterHandler(TPacketType packetType, Func<IClient, ValueTask>> handler)
//     {
//         return false;
//     }
//
//     // TODO: Should TMessage or IMessage?
//     bool RegisterHandler(TPacketType packetType, Func<IClient, TMessage, ValueTask>> handler)
//     {
//         return false;
//     }
// }

// TODO: How to add Server to this? so that a packet handler can interact with the state around it etc?

// public interface IServer
// {
//
// }