using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GET_CASH_SIZE_SEND (CLIENT.h:345-348) — query the real cash balance via ts25extra IPC.
///     <c>Sort</c> is echoed verbatim in ZC_GET_CASH_SIZE_RECV (client-side UI routing).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetCashSizeSend, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzGetCashSizeSend : IIncomingPacket<CzGetCashSizeSend>
{
    public required int Sort { get; init; }
}
