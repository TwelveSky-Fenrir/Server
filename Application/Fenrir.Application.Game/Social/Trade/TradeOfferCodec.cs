using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Social.Trade;

/// <summary>Projects a TradeOfferSide onto ZC_TRADE_START_RECV/ZC_TRADE_STATE_RECV's flattened wire arrays.</summary>
/// <remarks>
///     Slot layout ([0]=ItemId, [1]=Quantity, [2]=packed upgrade bytes, [3]=ExpireDate) is a documented
///     inference by analogy with AvatarInfoFactory.PackUpgradeBytes, not an independently verified layout.
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
