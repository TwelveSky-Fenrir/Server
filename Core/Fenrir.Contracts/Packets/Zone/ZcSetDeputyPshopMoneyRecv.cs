using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_SET_DEPUTY_PSHOP_MONEY_RECV (ZONE.h:1319-1324) — reply to CZ_SET_DEPUTY_PSHOP_MONEY_SEND.
///     <c>Result</c> 0 = ok (amounts withdrawn), 1-4 = error codes (proxy state, currency caps, IPC).
///     Unicast; same zone restriction as ZC_GET_DEPUTY_PSHOP_RECV.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SetDeputyPshopMoneyRecv,
    ExpectedSize = 13)]
public readonly partial record struct ZcSetDeputyPshopMoneyRecv : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Money { get; init; }
    public required int BigMoney { get; init; }
}
