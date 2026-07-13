using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class RuneSocketHandler(IRuneSocketService runeSocketService, ILogger<RuneSocketHandler> logger)
    : IAsyncPacketHandler<RuneSocketRequest>
{
    public async ValueTask HandleAsync(RuneSocketRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: RuneSocketRequest (op157) received for character {CharacterId}, sort {Sort} runeIndex {RuneIndex}",
            session.SessionId, characterId, packet.Sort, packet.RuneIndex);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            switch (packet.Sort)
            {
                case 0:
                {
                    var result = await runeSocketService.InsertAsync(packet, zone, state, characterId,
                        cancellationToken);

                    if (result.Outcome != RuneSocketOutcome.Applied)
                    {
                        logger.LogWarning(
                            "Rune insert rejected for character {CharacterId}: runeIndex {RuneIndex} -- aborting session",
                            characterId, packet.RuneIndex);
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    logger.LogInformation(
                        "Character {CharacterId} inserted rune {RuneIndex} into container {Page} slot {Index}",
                        characterId, packet.RuneIndex, packet.Page, packet.Index);

                    session.Send(new RuneSocketResponse
                    {
                        Result = 0, Page = packet.Page, Index = packet.Index, ItemIndex = packet.ItemIndex,
                        RuneIndex = packet.RuneIndex
                    });
                    return;
                }
                case 1:
                {
                    var result = await runeSocketService.RemoveAsync(packet, zone, state, characterId,
                        cancellationToken);

                    switch (result.Outcome)
                    {
                        case RuneSocketOutcome.Rejected:
                            logger.LogWarning(
                                "Rune remove rejected for character {CharacterId}: runeIndex {RuneIndex} -- aborting session",
                                characterId, packet.RuneIndex);
                            zoneSession.Abort(DisconnectReason.Faulted);
                            return;
                        case RuneSocketOutcome.InventoryFull:
                            logger.LogInformation(
                                "Rune remove denied for character {CharacterId}: inventory full (runeIndex {RuneIndex})",
                                characterId, packet.RuneIndex);
                            session.Send(new RuneSocketResponse
                            {
                                Result = 2, Page = 0, Index = 0, ItemIndex = 0, RuneIndex = 0
                            });
                            return;
                    }

                    logger.LogInformation(
                        "Character {CharacterId} removed rune {RuneIndex} into container {Page} slot {Index}",
                        characterId, packet.RuneIndex, result.Page, result.Index);

                    var granted = result.GrantedItem!.Value;
                    session.Send(new AddInventoryItemResponse
                    {
                        Result = 0,
                        ItemIndex = granted.ItemId,
                        Page = result.Page,
                        Index = result.Index,
                        Xy = 0,
                        Quantity = granted.Quantity,
                        Value = 0,
                        Serial = granted.Serial,
                        Socket = [0, 0, 0],
                        Expire = granted.ExpireDate
                    });

                    session.Send(new RuneSocketResponse
                    {
                        Result = 1, Page = result.Page, Index = result.Index, ItemIndex = result.ItemIndex,
                        RuneIndex = packet.RuneIndex
                    });
                    return;
                }
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
