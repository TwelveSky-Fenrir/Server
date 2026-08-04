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
        if (!trades.TryGetSession(characterId, out var expectedTrade) || expectedTrade is null)
        {
            logger.LogDebug("Trade lock ignored: character {CharacterId} has no active trade session",
                characterId);
            return new TradeLockAttempt(false, null);
        }

        var opponentId = expectedTrade.OpponentOf(characterId);
        var transition = trades.TryEnterTransition(characterId, opponentId);
        if (transition is null)
        {
            logger.LogDebug(
                "Trade lock ignored: character {CharacterId} / counterpart {OpponentId} already have an in-flight transition",
                characterId, opponentId);
            return new TradeLockAttempt(false, null);
        }

        using (transition)
        {
            if (!trades.TryGetSession(characterId, out var trade) || trade is null ||
                !ReferenceEquals(trade, expectedTrade))
            {
                logger.LogDebug("Trade lock ignored: character {CharacterId} has no active trade session", characterId);
                return new TradeLockAttempt(false, null);
            }

            if (!zones.TryGetPlayerAndZone(characterId, out var player, out var playerZone) || player.IsMovingZone ||
                !playerZone.TryGetPlayer(opponentId, out var opponent) || opponent is null || opponent.IsMovingZone)
            {
                logger.LogDebug(
                    "Trade lock ignored: character {CharacterId}'s counterpart {OpponentId} is unreachable, in another zone, or mid zone-transfer -- no notch advanced",
                    characterId, opponentId);
                return new TradeLockAttempt(false, null);
            }

            if (!trade.TryAdvanceConfirmation(characterId))
            {
                logger.LogDebug(
                    "Trade lock ignored: character {CharacterId} cannot advance its confirmation notch yet",
                    characterId);
                return new TradeLockAttempt(false, null);
            }

            var side = trade.SideOf(characterId);
            logger.LogDebug("Trade lock notch: character {CharacterId} menu state now {MenuState}", characterId,
                side.MenuState);
            return new TradeLockAttempt(true, trade);
        }
    }

    public async ValueTask CommitAsync(TradeSession trade, PlayerRuntimeState playerA, Zone zoneA,
        PlayerRuntimeState playerB, Zone zoneB, int characterId, CancellationToken cancellationToken)
    {
        var transition = trades.TryEnterTransition(trade.PlayerAId, trade.PlayerBId);
        if (transition is null)
        {
            logger.LogDebug(
                "Trade commit ignored: character {PlayerAId}/{PlayerBId} already have an in-flight transition",
                trade.PlayerAId, trade.PlayerBId);
            return;
        }

        using (transition)
        {
            if (!trades.TryBeginCommit(trade))
            {
                logger.LogDebug(
                    "Trade commit ignored: character {PlayerAId}/{PlayerBId} no longer own one fully-confirmed active session",
                    trade.PlayerAId, trade.PlayerBId);
                return;
            }

            var offerA = trade.SnapshotSide(trade.PlayerAId);
            var offerB = trade.SnapshotSide(trade.PlayerBId);

            if (!HasValidatedLiveParticipants(trade, playerA, zoneA, playerB, zoneB) ||
                !IsValidOffer(offerA) || !IsValidOffer(offerB))
            {
                logger.LogWarning(
                    "Trade commit rejected: character {PlayerAId}/{PlayerBId} failed live-participant or offer validation -- no database mutation attempted",
                    trade.PlayerAId, trade.PlayerBId);
                AbortStagedTrade(trade, playerA, playerB);
                return;
            }

            var planA = TradeCommitPlanner.BuildFinalContainers(
                playerA.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
                playerA.Inventory.GetContainer(ContainerMatrix.InventoryPage1),
                offerA.Slots, offerB.Slots, worldData);

            var planB = TradeCommitPlanner.BuildFinalContainers(
                playerB.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
                playerB.Inventory.GetContainer(ContainerMatrix.InventoryPage1),
                offerB.Slots, offerA.Slots, worldData);

            if (planA.Overflowed || planB.Overflowed)
            {
                logger.LogInformation(
                    "Trade commit rejected: character {PlayerAId}/{PlayerBId} plan unusable (side A rejection {PlanARejection}, side B rejection {PlanBRejection}) -- no database mutation attempted",
                    trade.PlayerAId, trade.PlayerBId, planA.Rejection, planB.Rejection);
                AbortStagedTrade(trade, playerA, playerB);
                return;
            }

            var moneyDeltaA = offerB.Money - offerA.Money;
            var bigMoneyDeltaA = offerB.BigMoney - offerA.BigMoney;
            var moneyDeltaB = offerA.Money - offerB.Money;
            var bigMoneyDeltaB = offerA.BigMoney - offerB.BigMoney;
            var tradeToken = TradeCommitToken.NewForCommit();

            try
            {
                await tradeCommits.ExecuteIdempotentAsync(
                    tradeToken,
                    trade.PlayerAId, ToTvps(planA.Page0), ToTvps(planA.Page1), moneyDeltaA, bigMoneyDeltaA,
                    trade.PlayerBId, ToTvps(planB.Page0), ToTvps(planB.Page1), moneyDeltaB, bigMoneyDeltaB,
                    cancellationToken,
                    ToTradedTvps(offerA), ToTradedTvps(offerB),
                    offerA.Money, offerA.BigMoney, offerB.Money, offerB.BigMoney);
            }
            catch (Exception ex) when (TryGetRecoverableCommitError(ex, out var errorNumber))
            {
                logger.LogWarning(ex,
                    "Trade commit rejected by SQL error {ErrorNumber} ({Reason}): offender character {OffenderId} (character {PlayerAId} net {MoneyDeltaA}/{BigMoneyDeltaA}, character {PlayerBId} net {MoneyDeltaB}/{BigMoneyDeltaB}) -- transaction rolled back",
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
                    "Trade commit outcome unknown: character {PlayerAId}/{PlayerBId} ExecuteIdempotentAsync threw after dispatch. The transition remains closed to protect balances; reconciliation requires a token-outcome read API.",
                    trade.PlayerAId, trade.PlayerBId);
                throw;
            }

            if (!trades.TryCompleteCommit(trade))
            {
                logger.LogCritical(
                    "Trade commit persistence succeeded but the active in-memory session was no longer present: character {PlayerAId}/{PlayerBId}",
                    trade.PlayerAId, trade.PlayerBId);
                return;
            }

            try
            {
                await PostMirrorAndWaitAsync(zoneA, playerA.CharacterId, planA, cancellationToken);
                await PostMirrorAndWaitAsync(zoneB, playerB.CharacterId, planB, cancellationToken);

                if (offerB.BigMoney != 0)
                    await zoneA.PostTribeProgressCommandAndWaitAsync(
                        new TribeProgressZoneCommand(playerA.CharacterId, BigMoneyDelta: offerB.BigMoney),
                        cancellationToken);
                if (offerA.BigMoney != 0)
                    await zoneB.PostTribeProgressCommandAndWaitAsync(
                        new TribeProgressZoneCommand(playerB.CharacterId, BigMoneyDelta: offerA.BigMoney),
                        cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Trade commit persisted but an in-memory mirror failed: character {PlayerAId}/{PlayerBId} must reload from storage",
                    trade.PlayerAId, trade.PlayerBId);
                return;
            }

            logger.LogInformation(
                "Trade committed: character {PlayerAId} money delta {MoneyDeltaA}/{BigMoneyDeltaA} ({ItemsReceivedByA} items received), " +
                "character {PlayerBId} money delta {MoneyDeltaB}/{BigMoneyDeltaB} ({ItemsReceivedByB} items received)",
                trade.PlayerAId, moneyDeltaA, bigMoneyDeltaA, offerB.Slots.Count(s => s is not null),
                trade.PlayerBId, moneyDeltaB, bigMoneyDeltaB, offerA.Slots.Count(s => s is not null));

            var result = new TradeEndResponse { Result = 0 };
            playerA.Session.Send(result);
            playerB.Session.Send(result);
        }
    }

    private void AbortStagedTrade(TradeSession trade, PlayerRuntimeState playerA, PlayerRuntimeState playerB)
    {
        if (!trades.TryAbortCommit(trade))
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

    private bool HasValidatedLiveParticipants(TradeSession trade, PlayerRuntimeState playerA, Zone zoneA,
        PlayerRuntimeState playerB, Zone zoneB)
    {
        if (playerA.CharacterId != trade.PlayerAId || playerB.CharacterId != trade.PlayerBId ||
            !ReferenceEquals(zoneA, zoneB) || playerA.IsMovingZone || playerB.IsMovingZone ||
            !zoneA.TryGetPlayer(playerA.CharacterId, out var liveA) ||
            !zoneB.TryGetPlayer(playerB.CharacterId, out var liveB) ||
            !ReferenceEquals(liveA, playerA) || !ReferenceEquals(liveB, playerB))
            return false;

        return true;
    }

    private bool IsValidOffer(TradeOfferSnapshot offer)
    {
        if (offer.Money is < 0 or > TradeMoneyPlacementResolver.MoneyCeiling ||
            offer.BigMoney < 0 || (long)offer.BigMoney > TradeBigMoneyPlacementResolver.BigMoneyCap)
            return false;

        foreach (var offered in offer.Slots)
        {
            if (offered is not { } slot ||
                slot.Container is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
                slot.Slot > 63 || slot.Stack.ItemId < 1 || slot.Stack.Quantity < 1 ||
                !worldData.ItemsById.TryGetValue(slot.Stack.ItemId, out var definition) ||
                definition.Item.CheckAvatarTrade == 1)
                return false;

            if (ContainerMatrix.IsStackableSort(definition.Item.Sort))
            {
                if (slot.Stack.Quantity > TradeItemPlacementResolver.MaxQuantity)
                    return false;
            }
            else if (slot.Stack.Quantity != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static List<CharacterItemSlotTvp> ToTradedTvps(TradeOfferSnapshot offer)
    {
        var list = new List<CharacterItemSlotTvp>(TradeLimits.SlotCount);
        for (byte i = 0; i < TradeLimits.SlotCount; i++)
        {
            if (offer.Slots[i] is not { } slot)
                continue;
            list.Add(slot.Stack.ToTvp(i));
        }

        return list;
    }
}
