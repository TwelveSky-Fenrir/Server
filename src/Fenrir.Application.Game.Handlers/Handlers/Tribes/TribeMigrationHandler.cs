using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribeMigrationHandler(
    ITribeMigrationService tribeMigrationService,
    ILogger<TribeMigrationHandler>? logger = null) : IAsyncPacketHandler<TribeMigrationRequest>
{
    public async ValueTask HandleAsync(TribeMigrationRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug("Session {SessionId}: CZ_CHANGE_TO_TRIBE4_SEND received (character {CharacterId})",
            session.SessionId, zoneSession.CharacterId);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        TribeMigrationOutcome outcome;
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            outcome = await tribeMigrationService.ConvertAsync(zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }

        switch (outcome)
        {
            case TribeMigrationOutcome.Success:
                session.Send(new TribeMigrationResponse { Result = 0 });
                return;
            case var repliesWithFailure when repliesWithFailure.RepliesWithFailure():
                session.Send(new TribeMigrationResponse { Result = 1 });
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }
}
