using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class StellarCoreStateHandler(IStellarCoreStateService service, ILogger<StellarCoreStateHandler> logger)
    : IAsyncPacketHandler<StellarCoreStateRequest>
{
    public async ValueTask HandleAsync(StellarCoreStateRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: StellarCoreStateRequest (op153) received for character {CharacterId}, sort {Sort} value {Value}",
            session.SessionId, characterId, packet.Sort, packet.Value);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await service.ApplyAsync(zone, state, characterId, packet.Sort, packet.Value,
                cancellationToken);

            switch (result.Outcome)
            {
                case StellarCoreStateOutcome.NoReply:
                    return;

                case StellarCoreStateOutcome.Disconnect:
                    logger.LogWarning(
                        "Stellar-core-state rejected for character {CharacterId}: sort {Sort} value {Value} -- aborting session",
                        characterId, packet.Sort, packet.Value);
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;

                case StellarCoreStateOutcome.Reply:
                    session.Send(new StellarCoreStateResponse
                    {
                        Result = result.ResultCode, Sort = packet.Sort, Value = packet.Value, Page = result.Page,
                        PosX = result.PosX, PosY = result.PosY, ItemIndex = result.ItemIndex
                    });
                    return;
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
