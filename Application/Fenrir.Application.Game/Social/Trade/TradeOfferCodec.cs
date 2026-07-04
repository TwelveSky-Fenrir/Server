using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Social.Trade;

/// <summary>
///     Projects a <see cref="TradeOfferSide" /> onto ZC_TRADE_START_RECV/ZC_TRADE_STATE_RECV's flattened
///     wire arrays (contracts/05_social.md: <c>Trade[8][4]</c>, <c>TradeSocket[8][3]</c>).
/// </summary>
/// <remarks>
///     OPEN ISSUE: the exact meaning of each slot's 4 ints is NOT broken down in contracts/05_social.md
///     (unlike AvatarInfo.Equip, which report 11 §2 documents field-by-field) and this pass did not
///     re-derive it from the raw C++ TRADE struct byte-for-byte. Modeled here as
///     [0]=ItemId, [1]=Quantity, [2]=packed upgrade bytes (same <c>SetISIUIMValue</c> packing
///     <c>AvatarInfoFactory.PackUpgradeBytes</c> already uses for Equip), [3]=ExpireDate -- a reasonable,
///     DOCUMENTED inference by analogy, not an independently verified layout.
/// </remarks>
public static class TradeOfferCodec
{
    public static TradeStartResponse BuildStart(TradeOfferSide side)
    {
        return new TradeStartResponse
        {
            TradeMoney = (int)side.Money,
            Trade = BuildTradeArray(side),
            TradeSocket = BuildSocketArray(side),
            BigTradeMoney = side.BigMoney
        };
    }

    public static TradeUpdateResponse BuildUpdate(TradeOfferSide side)
    {
        return new TradeUpdateResponse
        {
            TradeMoney = (int)side.Money,
            Trade = BuildTradeArray(side),
            TradeSocket = BuildSocketArray(side),
            BigTradeMoney = side.BigMoney
        };
    }

    private static int[] BuildTradeArray(TradeOfferSide side)
    {
        var trade = new int[TradeLimits.SlotCount * 4];
        for (var i = 0; i < TradeLimits.SlotCount; i++)
        {
            if (side.Slots[i] is not { } slot)
                continue;

            var stack = slot.Stack;
            var baseIndex = i * 4;
            trade[baseIndex] = stack.ItemId;
            trade[baseIndex + 1] = stack.Quantity;
            trade[baseIndex + 2] = stack.Enchant | (stack.Combine << 8) | (stack.Refine << 16) | (stack.Socket << 24);
            trade[baseIndex + 3] = stack.ExpireDate;
        }

        return trade;
    }

    private static int[] BuildSocketArray(TradeOfferSide side)
    {
        var sockets = new int[TradeLimits.SlotCount * 3];
        for (var i = 0; i < TradeLimits.SlotCount; i++)
        {
            if (side.Slots[i] is not { } slot)
                continue;

            var stack = slot.Stack;
            var baseIndex = i * 3;
            sockets[baseIndex] = stack.SocketGem1;
            sockets[baseIndex + 1] = stack.SocketGem2;
            sockets[baseIndex + 2] = stack.SocketGem3;
        }

        return sockets;
    }
}
