using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

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
/// </remarks>
public sealed class RuneSocketHandler(IRuneSocketService runeSocketService)
    : IAsyncPacketHandler<RuneSocketRequest>
{
    public async ValueTask HandleAsync(RuneSocketRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

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
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

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
                            zoneSession.Abort(DisconnectReason.Faulted);
                            return;
                        case RuneSocketOutcome.InventoryFull:
                            session.Send(new RuneSocketResponse
                            {
                                Result = 2, Page = 0, Index = 0, ItemIndex = 0, RuneIndex = 0
                            });
                            return;
                    }

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
