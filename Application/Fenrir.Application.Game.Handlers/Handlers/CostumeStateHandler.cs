using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CostumeStateHandler(ICostumeStateService service, ILogger<CostumeStateHandler> logger)
    : IAsyncPacketHandler<CostumeStateRequest>
{
    public async ValueTask HandleAsync(CostumeStateRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "Session {SessionId}: CostumeStateRequest (op90) received for character {CharacterId}, sort {Sort} value {Value}",
            session.SessionId, characterId, packet.Sort, packet.Value);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await service.ApplyAsync(zone, state, characterId, accountId, packet.Sort, packet.Value,
                cancellationToken);

            switch (result.Outcome)
            {
                case CostumeStateOutcome.NoReply:
                    return;

                case CostumeStateOutcome.Disconnect:
                    logger.LogWarning(
                        "Costume-state rejected for character {CharacterId}: sort {Sort} value {Value} -- aborting session",
                        characterId, packet.Sort, packet.Value);
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;

                case CostumeStateOutcome.Reply:
                    session.Send(new CostumeStateResponse
                    {
                        Result = result.ResultCode, Sort = packet.Sort, Value = packet.Value, Page = result.Page,
                        PosX = result.PosX, PosY = result.PosY, ItemIndex = result.ItemIndex, CostumeDate = 0
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
