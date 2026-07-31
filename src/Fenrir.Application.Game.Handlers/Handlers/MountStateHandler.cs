using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class MountStateHandler(IMountStateService service, ILogger<MountStateHandler> logger)
    : IAsyncPacketHandler<MountStateRequest>
{
    public async ValueTask HandleAsync(MountStateRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: MountStateRequest (op87) received for character {CharacterId}, sort {Sort} value {Value}",
            session.SessionId, characterId, packet.Sort, packet.Value);

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        MountStateResult result;
        try
        {
            result = await service.ApplyAsync(zone, state, characterId, accountId, packet.Sort, packet.Value,
                cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }

        switch (result.Outcome)
        {
            case MountStateOutcome.NoReply:
                return;

            case MountStateOutcome.Disconnect:
                logger.LogWarning(
                    "Mount-state rejected for character {CharacterId}: sort {Sort} value {Value} -- aborting session",
                    characterId, packet.Sort, packet.Value);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case MountStateOutcome.Select:
            case MountStateOutcome.Deselect:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;

            case MountStateOutcome.Mount:
                logger.LogInformation("Character {CharacterId} mounted animal slot {Value}", characterId,
                    packet.Value);
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;

            case MountStateOutcome.Dismount:
                logger.LogInformation("Character {CharacterId} dismounted", characterId);
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = 0 });
                return;

            case MountStateOutcome.DeleteMount:
            case MountStateOutcome.DeleteAttribute:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;
        }
    }
}
