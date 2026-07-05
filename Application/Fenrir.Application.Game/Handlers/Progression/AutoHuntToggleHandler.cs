using Fenrir.Application.Game.Handlers.Progression.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_AUTO_CONFIG_SEND (opcode 99) -- auto-hunt on/off toggle. Enabling requires an equipped weapon and a
///     configured attack skill. OPEN ISSUE: legacy also mentions unenumerated "level-gated battle zones", not
///     modeled here.
/// </summary>
public sealed class AutoHuntToggleHandler(IAutoHuntToggleService autoHuntToggleService)
    : IAsyncPacketHandler<AutoHuntToggleRequest>
{
    public async ValueTask HandleAsync(AutoHuntToggleRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var result = await autoHuntToggleService.ToggleAsync(characterId, zone, state, packet, cancellationToken);

        if (result.Aborted)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new AutoHuntToggleResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, AutoState = packet.Sort
        });
    }
}
