using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeLockService(
    TradeRegistry trades,
    ZoneRegistry zones,
    ITradeCommitRepository tradeCommits,
    WorldDataCache worldData,
    ILogger<TradeLockService> logger) : ITradeLockService
{
    private const int InsufficientBalanceCharacterAError = 50268;

    private const int InsufficientBalanceCharacterBError = 50269;

    private const int CurrencyCapCharacterAError = 50362;

    private const int CurrencyCapCharacterBError = 50363;

    public TradeLockAttempt TryLock(int characterId)
    {
        if (!trades.TryGetSession(characterId, out var trade) || trade is null)
        {
            logger.LogDebug("Trade lock ignored: character {CharacterId} has no active trade session",
                characterId);
            return new TradeLockAttempt(false, null);
        }

        var opponentId = trade.OpponentOf(characterId);
        if (!zones.TryGetPlayer(opponentId, out var opponent) || opponent.IsMovingZone)
        {
            logger.LogDebug(
                "Trade lock ignored: character {CharacterId}'s counterpart {OpponentId} is unreachable or mid zone-transfer -- no notch advanced",
                characterId, opponentId);
            return new TradeLockAttempt(false, null);
        }

        if (!trade.CanAdvanceConfirmation(characterId))
        {
            logger.LogDebug(
                "Trade lock ignored: character {CharacterId} cannot advance its confirmation notch yet", characterId);
            return new TradeLockAttempt(false, null);
        }

        var side = trade.SideOf(characterId);

        side.MenuState++;
        logger.LogDebug("Trade lock notch: character {CharacterId} menu state now {MenuState}", characterId,
            side.MenuState);
        return new TradeLockAttempt(true, trade);
    }

    public async ValueTask CommitAsync(TradeSession trade, PlayerRuntimeState playerA, Zone zoneA,
        PlayerRuntimeState playerB, Zone zoneB, int characterId, CancellationToken cancellationToken)
    {
        if (playerA.IsMovingZone || playerB.IsMovingZone)
        {
            logger.LogInformation(
                "Trade commit aborted: character {PlayerAId} (moving zone {PlayerAMoving}) / character {PlayerBId} (moving zone {PlayerBMoving}) entered a zone transfer after both sides confirmed -- nothing written, session left for the disconnect path to tear down",
                trade.PlayerAId, playerA.IsMovingZone, trade.PlayerBId, playerB.IsMovingZone);
            return;
        }

        var planA = TradeCommitPlanner.BuildFinalContainers(
            playerA.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
            playerA.Inventory.GetContainer(ContainerMatrix.InventoryPage1),
            trade.SideA.Slots, trade.SideB.Slots, worldData);

        var planB = TradeCommitPlanner.BuildFinalContainers(
            playerB.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
            playerB.Inventory.GetContainer(ContainerMatrix.InventoryPage1),
            trade.SideB.Slots, trade.SideA.Slots, worldData);

        if (planA.Overflowed || planB.Overflowed)
        {
            logger.LogInformation(
                "Trade commit rejected: character {PlayerAId}/{PlayerBId} plan unusable (side A rejection {PlanARejection}, side B rejection {PlanBRejection}) -- both menus reset to locked for retry",
                trade.PlayerAId, trade.PlayerBId, planA.Rejection, planB.Rejection);
            AbortStagedTrade(trade, playerA, playerB);
            return;
        }

        var moneyDeltaA = trade.SideB.Money - trade.SideA.Money;
        var bigMoneyDeltaA = trade.SideB.BigMoney - trade.SideA.BigMoney;
        var moneyDeltaB = trade.SideA.Money - trade.SideB.Money;
        var bigMoneyDeltaB = trade.SideA.BigMoney - trade.SideB.BigMoney;

        var tradeToken = TradeCommitToken.NewForCommit();

        try
        {
            await tradeCommits.ExecuteIdempotentAsync(
                tradeToken,
                trade.PlayerAId, ToTvps(planA.Page0), ToTvps(planA.Page1), moneyDeltaA, bigMoneyDeltaA,
                trade.PlayerBId, ToTvps(planB.Page0), ToTvps(planB.Page1), moneyDeltaB, bigMoneyDeltaB,
                cancellationToken,
                ToTradedTvps(trade.SideA), ToTradedTvps(trade.SideB),
                trade.SideA.Money, trade.SideA.BigMoney, trade.SideB.Money, trade.SideB.BigMoney);
        }
        catch (Exception ex) when (TryGetRecoverableCommitError(ex, out var errorNumber))
        {
            logger.LogWarning(ex,
                "Trade commit rejected by SQL error {ErrorNumber} ({Reason}): offender character {OffenderId} (character {PlayerAId} net {MoneyDeltaA}/{BigMoneyDeltaA}, character {PlayerBId} net {MoneyDeltaB}/{BigMoneyDeltaB}) -- transaction rolled back, both menus reset to locked",
                errorNumber,
                errorNumber is CurrencyCapCharacterAError or CurrencyCapCharacterBError
                    ? "would exceed the legacy currency cap"
                    : "staged more money than it owns",
                errorNumber is InsufficientBalanceCharacterAError or CurrencyCapCharacterAError
                    ? trade.PlayerAId
                    : trade.PlayerBId,
                trade.PlayerAId, moneyDeltaA, bigMoneyDeltaA, trade.PlayerBId, moneyDeltaB, bigMoneyDeltaB);

            AbortStagedTrade(trade, playerA, playerB);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Trade commit failed: character {PlayerAId}/{PlayerBId} ExecuteIdempotentAsync threw -- both sessions will be torn down",
                trade.PlayerAId, trade.PlayerBId);
            throw;
        }

        await PostMirrorAndWaitAsync(zoneA, playerA.CharacterId, planA, cancellationToken);
        await PostMirrorAndWaitAsync(zoneB, playerB.CharacterId, planB, cancellationToken);

        var bigMoneyReceivedByA = trade.SideB.BigMoney;
        var bigMoneyReceivedByB = trade.SideA.BigMoney;

        if (bigMoneyReceivedByA != 0)
            await zoneA.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(playerA.CharacterId, BigMoneyDelta: bigMoneyReceivedByA),
                cancellationToken);
        if (bigMoneyReceivedByB != 0)
            await zoneB.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(playerB.CharacterId, BigMoneyDelta: bigMoneyReceivedByB),
                cancellationToken);

        trades.TryEnd(characterId, out _);

        logger.LogInformation(
            "Trade committed: character {PlayerAId} money delta {MoneyDeltaA}/{BigMoneyDeltaA} ({ItemsReceivedByA} items received), " +
            "character {PlayerBId} money delta {MoneyDeltaB}/{BigMoneyDeltaB} ({ItemsReceivedByB} items received)",
            trade.PlayerAId, moneyDeltaA, bigMoneyDeltaA, trade.SideB.Slots.Count(s => s is not null),
            trade.PlayerBId, moneyDeltaB, bigMoneyDeltaB, trade.SideA.Slots.Count(s => s is not null));

        var result = new TradeEndResponse { Result = 0 };
        playerA.Session.Send(result);
        playerB.Session.Send(result);
    }

    private void AbortStagedTrade(TradeSession trade, PlayerRuntimeState playerA, PlayerRuntimeState playerB)
    {
        if (!trades.TryEnd(playerA.CharacterId, out _))
            return;

        RestoreStagedBigMoney(playerA, trade.SideA.BigMoney);
        RestoreStagedBigMoney(playerB, trade.SideB.BigMoney);

        var response = new TradeEndResponse { Result = 1 };
        playerA.Session.Send(response);
        playerB.Session.Send(response);
    }

    private void RestoreStagedBigMoney(PlayerRuntimeState player, int amount)
    {
        if (amount == 0)
            return;

        if (zones.TryGetPlayerAndZone(player.CharacterId, out _, out var zone))
            zone.PostTribeProgressCommand(new TribeProgressZoneCommand(player.CharacterId, BigMoneyDelta: amount));
    }

    private static bool TryGetRecoverableCommitError(Exception exception, out int errorNumber)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
                continue;

            for (var i = 0; i < sqlException.Errors.Count; i++)
            {
                var number = sqlException.Errors[i].Number;
                if (number is not (InsufficientBalanceCharacterAError or InsufficientBalanceCharacterBError
                    or CurrencyCapCharacterAError or CurrencyCapCharacterBError))
                    continue;

                errorNumber = number;
                return true;
            }
        }

        errorNumber = 0;
        return false;
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

    private static List<CharacterItemSlotTvp> ToTradedTvps(TradeOfferSide side)
    {
        var list = new List<CharacterItemSlotTvp>(TradeLimits.SlotCount);
        for (byte i = 0; i < TradeLimits.SlotCount; i++)
        {
            if (side.Slots[i] is not { } slot)
                continue;
            list.Add(slot.Stack.ToTvp(i));
        }

        return list;
    }
}
