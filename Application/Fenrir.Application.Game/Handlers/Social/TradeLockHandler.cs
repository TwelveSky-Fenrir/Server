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
///     CZ_TRADE_MENU_SEND (opcode 51) -- the 2-notch confirm machine (contracts/05_social.md): first call
///     locks (menu 0→1, ZC_TRADE_MENU_RECV CheckMe=0 to self/1 to other), second call confirms (1→2, same
///     echo shape -- a reasonable inference: the contract's own text only spells out the FIRST
///     transition's echo, open issue). Once BOTH sides reach menu==2, this handler performs the ATOMIC
///     two-character commit (<see cref="CharacterRepository.ExecuteTradeAsync" />, D7 regime (b)) THEN --
///     single-writer invariant, exactly like <c>GenericActionHandler</c> -- posts each side's own
///     ALREADY-DURABLE new container contents back onto THEIR OWN zone via
///     <see cref="Zone.PostInventoryCommandAndWaitAsync" /> rather than mutating
///     <see cref="PlayerRuntimeState.Inventory" /> directly from this request thread; the zone's own tick
///     performs the actual mirror. Ends the session with ZC_TRADE_END_RECV Result=0 to both. An overflow
///     (the receiving side has no free inventory slot for an incoming item,
///     <see cref="TradeCommitPlanner.Plan.Overflowed" />) aborts the WHOLE commit -- no SQL call is made, no
///     partial state, matching D7's "no partial commit" standard exactly like a mid-write SQL fault would
///     have to.
/// </summary>
/// <remarks>
///     Review finding (Phase C/V6): the commit section used to read BOTH players' live
///     <see cref="PlayerRuntimeState.Inventory" /> (potentially hosted by two different zones/tick threads)
///     with no synchronization at all -- any unrelated inventory-affecting event for either party between
///     "plan built" and "SQL commit" (a loot pickup, a hotkey potion use, an NPC purchase) would be silently
///     overwritten by the commit's own unconditional whole-container replace. Both players'
///     <see cref="PlayerRuntimeState.EconomyActionLock" /> are now acquired (in a FIXED order -- the smaller
///     CharacterId first, always, regardless of which side's request triggers the commit -- to rule out a
///     lock-ordering deadlock against a hypothetical future two-lock caller) around the ENTIRE plan/commit/
///     mirror-wait sequence, the exact same lock every other economy handler
///     (<c>GenericActionHandler</c>/<c>EnchantItemHandler</c>/<c>CraftItemHandler</c>) already uses for its
///     own single-character version of this same race.
/// </remarks>
public sealed class TradeLockHandler(ZoneRegistry zones, TradeRegistry trades, CharacterRepository characters)
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

        // Fixed acquisition order (smaller CharacterId first) regardless of which side's own request reached
        // menu==2 last -- see class remarks on why this rules out a lock-ordering deadlock.
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
            // D7 "no partial commit": abort the whole trade rather than silently drop an item or
            // partially apply it. No error code exists on the wire for this case -- reset both menus to
            // locked (1) so the players can free up space and re-confirm, matching the least-surprising
            // legacy-adjacent behavior absent a documented specific response.
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

    /// <summary>Single-writer invariant: the durable result is already committed to SQL above -- this asks that player's OWN zone tick to mirror it into <see cref="PlayerRuntimeState.Inventory" /> and WAITS for that mirror to actually apply (not merely be posted), same posture as <c>GenericActionHandler</c>'s own <see cref="Zone.PostInventoryCommandAndWaitAsync" /> calls -- see this type's own class remarks.</summary>
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
