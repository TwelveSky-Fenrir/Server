using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Social.Trade;

/// <summary>MAX_TRADE_SLOT_NUM/MAX_TRADE_VALUE_NUM/MAX_TRADE_GEM_NUM (DEFINE.h:291-293).</summary>
public static class TradeLimits
{
    public const int SlotCount = 8;
}

/// <summary>
///     One side's offer -- 8 item slots (each a reference to where in inventory the offered stack currently
///     lives, so commit can remove it from there) plus the two money pools. MenuState is CZ_TRADE_MENU_SEND's
///     own state machine: 0 open, 1 locked, 2 confirmed.
/// </summary>
public sealed class TradeOfferSide
{
    public readonly (byte Container, byte Slot, ItemStack Stack)?[] Slots =
        new (byte Container, byte Slot, ItemStack Stack)?[TradeLimits.SlotCount];

    public long Money { get; set; }
    public int BigMoney { get; set; }
    public int MenuState { get; set; }
}

/// <summary>
///     One active trade negotiation's shared state -- allocated at CZ_TRADE_START_SEND, both participants'
///     handlers resolve the same instance via TradeRegistry. Deliberately not persisted, matching the legacy.
/// </summary>
public sealed class TradeSession
{
    public required int PlayerAId { get; init; }
    public required int PlayerBId { get; init; }

    public TradeOfferSide SideA { get; } = new();
    public TradeOfferSide SideB { get; } = new();

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
}
