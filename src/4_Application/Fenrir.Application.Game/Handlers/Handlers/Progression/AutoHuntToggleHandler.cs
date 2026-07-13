using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Progression;

public sealed class AutoHuntToggleHandler(
    IAutoHuntToggleService autoHuntToggleService,
    ILogger<AutoHuntToggleHandler> logger)
    : IAsyncPacketHandler<AutoHuntToggleRequest>
{
    public async ValueTask HandleAsync(AutoHuntToggleRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: AutoHuntToggleRequest (op99) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var result = await autoHuntToggleService.ToggleAsync(characterId, zone, state, packet, cancellationToken);

        if (result.Aborted)
        {
            logger.LogWarning(
                "Auto-hunt toggle rejected for character {CharacterId} on map {MapId}: sort {Sort} -- aborting session",
                characterId, zone.MapId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        logger.LogInformation("Character {CharacterId} set auto-hunt {Enabled}", characterId, result.Enabled);

        session.Send(new AutoHuntToggleResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, AutoState = packet.Sort
        });
    }
}
