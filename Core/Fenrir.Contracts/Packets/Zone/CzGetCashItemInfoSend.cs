using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GET_CASH_ITEM_INFO_SEND (CLIENT.h:155-161) — request the cash-shop catalog. Empty payload
///     (header only). The server replies with ZC_GET_CASH_ITEM_INFO_RECV only when the client's cached
///     <c>mCashVersion</c> differs from <c>mCashInfo-&gt;mVersion</c> — otherwise total silence.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetCashItemInfoSend,
    ExpectedSize = 9, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzGetCashItemInfoSend : IIncomingPacket<CzGetCashItemInfoSend>
{
}
