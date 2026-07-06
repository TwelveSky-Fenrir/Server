using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.WithdrawProxyShopEarnings,
    ExpectedSize = 17, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct
    WithdrawProxyShopEarningsRequest : IIncomingPacket<WithdrawProxyShopEarningsRequest>
{
    public required int Money { get; init; }
    public required int BigMoney { get; init; }
}
