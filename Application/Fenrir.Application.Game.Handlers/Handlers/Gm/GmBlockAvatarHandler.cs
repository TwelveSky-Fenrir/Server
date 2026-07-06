using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Gm;

/// <summary>
///     Fenrir's dedicated GM-BLOCK command (legacy case 519 "[GM]-BLOCK",
///     Server/ts25zone/S04_MyWork04.cpp:1487-1515). Business logic lives in
///     <see cref="IGmBlockAvatarService" />.
/// </summary>
public sealed class GmBlockAvatarHandler(IGmBlockAvatarService service) : IAsyncPacketHandler<GmBlockAvatarRequest>
{
    public ValueTask HandleAsync(GmBlockAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        return service.HandleAsync(packet, (ZoneClientSession)session, cancellationToken);
    }
}
