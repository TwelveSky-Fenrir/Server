using System.Diagnostics.CodeAnalysis;
using Fenrir.LoginServer.Network.Metadata;
using Fenrir.LoginServer.Network.Protocol;
using Fenrir.Network.Factory;

namespace Fenrir.LoginServer.Network.Factory;

public class MessageFactory : IMessageFactory<PacketType, Packet>
{
    private readonly Dictionary<PacketType, Func<Packet>> _messages = new()
    {
        [PacketType.ServerZoneInfo] = () => new ServerZoneInfoRequestMessage()
    };

    public bool TryGetMessage(PacketType packetType, [NotNullWhen(true)] out Packet? message)
    {
        message = null;

        if (!_messages.TryGetValue(packetType, out var factory))
            return false;

        message = factory();
        return false;
    }
}