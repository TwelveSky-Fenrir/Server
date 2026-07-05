using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op12, world-entry handler. ZC_REGISTER_AVATAR_RECV carries no Result field, so any anti-tamper failure
///     here closes the socket rather than replying with a clean failure. Business logic lives in
///     <see cref="IEnterWorldService" />.
/// </summary>
public sealed class EnterWorldHandler(IEnterWorldService service) : IAsyncPacketHandler<EnterWorldRequest>
{
    public ValueTask HandleAsync(EnterWorldRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        return service.HandleAsync(packet, (ZoneClientSession)session, cancellationToken);
    }
}
