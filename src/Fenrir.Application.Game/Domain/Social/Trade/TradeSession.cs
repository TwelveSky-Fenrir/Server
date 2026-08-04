using Fenrir.Application.Game.Domain.Inventory;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Social.Trade;

public static class TradeLimits
{
    public const int SlotCount = 8;

    public const int FrozenMenuState = 1;

    public const int ConfirmedMenuState = 2;
}

public sealed class TradeOfferSide
{
    public readonly (byte Container, byte Slot, ItemStack Stack)?[] Slots =
        new (byte Container, byte Slot, ItemStack Stack)?[TradeLimits.SlotCount];

    public long Money { get; set; }
    public int BigMoney { get; set; }
    public int MenuState { get; set; }

    public bool IsOfferFrozen => MenuState >= TradeLimits.FrozenMenuState;

    public bool IsFullyConfirmed => MenuState >= TradeLimits.ConfirmedMenuState;

    public long GetOriginStagedQuantity(byte container, byte slot, int excludingTradeSlotIndex)
    {
        long total = 0;
        for (var i = 0; i < TradeLimits.SlotCount; i++)
        {
            if (i == excludingTradeSlotIndex)
                continue;

            if (Slots[i] is { } entry && entry.Container == container && entry.Slot == slot)
                total += entry.Stack.Quantity;
        }

        return total;
    }

    public bool ReservesOrigin(byte container, byte slot, int excludingTradeSlotIndex = -1)
    {
        for (var i = 0; i < TradeLimits.SlotCount; i++)
        {
            if (i == excludingTradeSlotIndex)
                continue;

            if (Slots[i] is { } entry && entry.Container == container && entry.Slot == slot)
                return true;
        }

        return false;
    }
}

public readonly record struct TradeOfferSnapshot(
    ImmutableArray<(byte Container, byte Slot, ItemStack Stack)?> Slots,
    long Money,
    int BigMoney);

public sealed class TradeSession
{
    private const int Open = 0;
    private const int Committing = 1;
    private const int Closed = 2;

    private int _commitState;

    public required int PlayerAId { get; init; }
    public required int PlayerBId { get; init; }

    public TradeOfferSide SideA { get; } = new();
    public TradeOfferSide SideB { get; } = new();

    public bool BothFullyConfirmed => SideA.IsFullyConfirmed && SideB.IsFullyConfirmed;

    public bool IsCommitInProgress => Volatile.Read(ref _commitState) == Committing;

    public TradeOfferSide SideOf(int characterId)
    {
        return characterId == PlayerAId ? SideA : SideB;
    }

    public TradeOfferSide OpponentSideOf(int characterId)
    {
        return characterId == PlayerAId ? SideB : SideA;
    }

    public int OpponentOf(int characterId)
    {
        return characterId == PlayerAId ? PlayerBId : PlayerAId;
    }

    public bool CanAdvanceConfirmation(int characterId)
    {
        var side = SideOf(characterId);
        var opponent = OpponentSideOf(characterId);

        return side.MenuState switch
        {
            0 => !opponent.IsFullyConfirmed,
            TradeLimits.FrozenMenuState => opponent.IsOfferFrozen,
            _ => false
        };
    }

    public bool TryAdvanceConfirmation(int characterId)
    {
        if (Volatile.Read(ref _commitState) != Open || !CanAdvanceConfirmation(characterId))
            return false;

        SideOf(characterId).MenuState++;
        return true;
    }

    public bool TryBeginCommit()
    {
        return BothFullyConfirmed &&
               Interlocked.CompareExchange(ref _commitState, Committing, Open) == Open;
    }

    public bool TryClose()
    {
        return Interlocked.CompareExchange(ref _commitState, Closed, Open) == Open;
    }

    public void CompleteCommit()
    {
        Interlocked.Exchange(ref _commitState, Closed);
    }

    public TradeOfferSnapshot SnapshotSide(int characterId)
    {
        var side = SideOf(characterId);
        return new TradeOfferSnapshot(ImmutableArray.CreateRange(side.Slots), side.Money, side.BigMoney);
    }
}
