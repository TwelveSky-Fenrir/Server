using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TRADE_MENU_SEND (opcode 51) -- the 2-notch confirm machine: first call locks (menu 0→1,
///     ZC_TRADE_MENU_RECV CheckMe=0 to self/1 to other), second call confirms (1→2, same echo shape --
///     an inference, since the contract only spells out the first transition's echo; open issue). Once
///     both sides reach menu==2, performs the atomic two-character commit
///     (<see cref="CharacterRepository.ExecuteTradeAsync" />, D7 regime (b)), then mirrors each side's new
///     container back onto their own zone (single-writer invariant, see remarks) and ends with
///     ZC_TRADE_END_RECV Result=0. An overflow (<see cref="TradeCommitPlanner.Plan.Overflowed" />) aborts
///     the whole commit -- no partial state (D7 "no partial commit").
/// </summary>
/// <remarks>
///     Both players' <see cref="PlayerRuntimeState.EconomyActionLock" /> are acquired, in a fixed order
///     (smaller CharacterId first, regardless of which side's request triggers the commit) to rule out a
///     lock-ordering deadlock, around the entire plan/commit/mirror-wait sequence -- same pattern as
///     <c>GenericActionHandler</c>/<c>EnchantItemHandler</c>/<c>CraftItemHandler</c> use for their own
///     single-character version of this race.
/// </remarks>
public sealed class TradeLockHandler(ZoneRegistry zones, TradeRegistry trades, ICharacterRepository characters)
    : IAsyncPacketHandler<TradeLockRequest>
{
    public async ValueTask HandleAsync(TradeLockRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (!trades.TryGetSession(characterId, out var trade) || trade is null)
            return;

        var side = trade.SideOf(characterId);
        if (side.MenuState >= 2)
            return;

        side.MenuState++;

        if (!zones.TryGetPlayerAndZone(trade.PlayerAId, out var playerA, out var zoneA) ||
            !zones.TryGetPlayerAndZone(trade.PlayerBId, out var playerB, out var zoneB))
            return;

        playerA.Session.Send(new TradeLockResponse { CheckMe = characterId == trade.PlayerAId ? 0 : 1 });
        playerB.Session.Send(new TradeLockResponse { CheckMe = characterId == trade.PlayerBId ? 0 : 1 });

        if (trade.SideA.MenuState < 2 || trade.SideB.MenuState < 2)
            return;

        // Fixed order (smaller CharacterId first) -- see class remarks on why this rules out deadlock.
        var (first, second) = playerA.CharacterId < playerB.CharacterId ? (playerA, playerB) : (playerB, playerA);

        await first.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await second.EconomyActionLock.WaitAsync(cancellationToken);
            try
            {
                await CommitAsync(trade, playerA, zoneA, playerB, zoneB, characterId, cancellationToken);
            }
            finally
            {
                second.EconomyActionLock.Release();
            }
        }
        finally
        {
            first.EconomyActionLock.Release();
        }
    }

    private async ValueTask CommitAsync(TradeSession trade, PlayerRuntimeState playerA, Zone zoneA,
        PlayerRuntimeState playerB, Zone zoneB, int characterId, CancellationToken cancellationToken)
    {
        var planA = TradeCommitPlanner.BuildFinalContainers(
            playerA.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
            playerA.Inventory.GetContainer(ContainerMatrix.InventoryPage1),
            trade.SideA.Slots, trade.SideB.Slots);

        var planB = TradeCommitPlanner.BuildFinalContainers(
            playerB.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
            playerB.Inventory.GetContainer(ContainerMatrix.InventoryPage1),
            trade.SideB.Slots, trade.SideA.Slots);

        if (planA.Overflowed || planB.Overflowed)
        {
            // D7 "no partial commit": abort rather than drop/partially apply an item. No wire error code
            // exists for this case, so reset both menus to locked (1) so players can free space and retry.
            trade.SideA.MenuState = 1;
            trade.SideB.MenuState = 1;
            return;
        }

        await characters.ExecuteTradeAsync(
            trade.PlayerAId, ToTvps(planA.Page0), ToTvps(planA.Page1),
            trade.SideB.Money - trade.SideA.Money, trade.SideB.BigMoney - trade.SideA.BigMoney,
            trade.PlayerBId, ToTvps(planB.Page0), ToTvps(planB.Page1),
            trade.SideA.Money - trade.SideB.Money, trade.SideA.BigMoney - trade.SideB.BigMoney,
            cancellationToken);

        await PostMirrorAndWaitAsync(zoneA, playerA.CharacterId, planA, cancellationToken);
        await PostMirrorAndWaitAsync(zoneB, playerB.CharacterId, planB, cancellationToken);

        trades.TryEnd(characterId, out _);

        var result = new TradeEndResponse { Result = 0 };
        playerA.Session.Send(result);
        playerB.Session.Send(result);
    }

    /// <summary>Mirrors the already-committed SQL result into the player's own zone and waits for it to apply (single-writer invariant, see class summary).</summary>
    private static async Task PostMirrorAndWaitAsync(Zone zone, int characterId, TradeCommitPlanner.Plan plan,
        CancellationToken cancellationToken)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, plan.Page0),
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, plan.Page1));

        await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
            cancellationToken);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
