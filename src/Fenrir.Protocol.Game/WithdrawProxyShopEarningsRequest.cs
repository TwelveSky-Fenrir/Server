using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.WithdrawProxyShopEarnings,
    ExpectedSize = 17, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct
    WithdrawProxyShopEarningsRequest : IIncomingPacket<WithdrawProxyShopEarningsRequest>
{
    public required int Money { get; init; }
    public required int BigMoney { get; init; }
}
