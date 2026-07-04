using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Social.Trade;

/// <summary>MAX_TRADE_SLOT_NUM/MAX_TRADE_VALUE_NUM/MAX_TRADE_GEM_NUM (DEFINE.h:291-293), verified against contracts/05_social.md's own ZC_TRADE_START_RECV table.</summary>
public static class TradeLimits
{
    public const int SlotCount = 8;
}

/// <summary>
///     One side's offer -- 8 item slots (each a reference to WHERE in that character's own inventory the
///     offered stack currently lives, so the atomic commit can remove it from there) plus the two money
///     pools. <see cref="MenuState" /> is the legacy's own CZ_TRADE_MENU_SEND 2-notch machine: 0 = open,
///     1 = locked, 2 = confirmed (contracts/05_social.md).
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
///     handlers resolve the SAME instance via <c>TradeRegistry</c>. Deliberately NOT persisted (matches
///     the legacy exactly -- transient trade state is never written to SQL, verified by
///     game.Characters_progression.sql's own header: "Deliberately NOT columns here: ... transient trade
///     state (never persisted, matching legacy)").
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
