using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_SET_DEPUTY_PSHOP_MONEY_SEND (CLIENT.h:470-474) — withdraw earnings from the deputy shop
///     (<c>mProxySystem.Process(..., 2)</c>). Reply: ZC_SET_DEPUTY_PSHOP_MONEY_RECV.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.WithdrawProxyShopEarnings,
    ExpectedSize = 17, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct WithdrawProxyShopEarningsRequest : IIncomingPacket<WithdrawProxyShopEarningsRequest>
{
    public required int Money { get; init; }
    public required int BigMoney { get; init; }
}
