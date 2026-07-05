using System.Collections.Immutable;
using Fenrir.Application.Game.Avatars;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Pets;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Guilds;
using Fenrir.Data.Security;
using Fenrir.Data.Social;
using Fenrir.Data.Tribes;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

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
