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
///     CZ_TRADE_MENU_SEND (opcode 51) -- 2-notch confirm: first call locks (menu 0→1), second confirms
///     (1→2). At menu==2 on both sides, commits atomically
///     (<see cref="CharacterRepository.ExecuteTradeAsync" />) and mirrors each side's new container back to
///     their own zone. An overflow aborts the whole commit -- no partial state.
/// </summary>
/// <remarks>
///     Both players' <see cref="PlayerRuntimeState.EconomyActionLock" /> are acquired in a fixed order
///     (smaller CharacterId first) to rule out lock-ordering deadlock.
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
            // No wire error code for overflow: reset both menus to locked (not cleared) so players can retry.
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
