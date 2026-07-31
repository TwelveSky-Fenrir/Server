using Fenrir.Application.Game.Domain.Inventory;

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
}

public sealed class TradeSession
{
    public required int PlayerAId { get; init; }
    public required int PlayerBId { get; init; }

    public TradeOfferSide SideA { get; } = new();
    public TradeOfferSide SideB { get; } = new();

    public bool BothFullyConfirmed => SideA.IsFullyConfirmed && SideB.IsFullyConfirmed;

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
}
