using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Social.Trade;

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
