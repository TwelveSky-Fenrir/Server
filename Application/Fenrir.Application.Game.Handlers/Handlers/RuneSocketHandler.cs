using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_RUNE_SYSTEM_SEND (op157). Sort 0 = inventory-&gt;rune (insert), Sort 1 = rune-&gt;inventory (remove),
///     both delegated to <see cref="IRuneSocketService" />. Any other Sort is silently ignored (no reply, no
///     disconnect -- the legacy switch has no default case, unlike almost every other opcode in this codebase).
///     See <c>RuneSocketResolver</c>'s remarks for the client-supplied-item-id quirk on insert.
/// </summary>
/// <remarks>
///     Result correction vs. an earlier reading of this contract: 0 = INSERT ok, 1 = REMOVE ok, 2 = inventory
///     full (removal only) -- <c>B_RUNE_SYSTEM_RECV(0, ...)</c> terminates the Sort=0 branch and
///     <c>B_RUNE_SYSTEM_RECV(1, ...)</c> terminates the Sort=1 branch in S04_MyWork03.cpp:8162, the reverse of
///     <see cref="RuneSocketResponse" />'s original doc comment. Stat recompute (SetBasicAbilityFromEquip +
///     SetHPMP) is skipped: <c>aRuneSystemStat</c> has no consumer anywhere in Fenrir's stat pipeline yet, so
///     recomputing today would be a pure no-op (same posture as <c>DrinkBottleHandler</c>'s own remarks).
///     A successful Sort=1 withdrawal sends <b>two</b> packets, not one: an <see cref="AddInventoryItemResponse" />
///     (ZC_ADD_USER_INVENTORY_ITEM_RECV) confirming the rune's resulting inventory position, immediately
///     followed by the <see cref="RuneSocketResponse" /> confirming the rune-slot withdrawal itself -- same
///     two-packet ordering/reason as <c>CraftItemHandler</c>'s Advanced Elixir/granted-item branches (client
///     learns of the new item before the result packet that references its slot).
/// </remarks>
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

                    // B_RUNE_SYSTEM_RECV(1, ...) describes the rune-slot withdrawal itself, not the granted
                    // inventory stack -- the granted item rides its own ZC_ADD_USER_INVENTORY_ITEM_RECV, sent
                    // first so the client learns of the new item before the rune-slot result references its
                    // resulting position. Same ordering/reason as CraftItemHandler's Advanced Elixir branch.
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
