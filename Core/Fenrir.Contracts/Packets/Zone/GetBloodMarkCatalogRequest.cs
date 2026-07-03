using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_DEMAND_BLOOD_MARK_SEND (CLIENT.h:160) — request the blood-mark catalog. Empty payload
///     (header only). Registered under <c>USE_BLOOD</c> (on in EU33). Silence if <c>mCashInfo</c> is
///     null. Reply: ZC_DEMAND_BLOOD_MARK_RECV (<c>mCashInfo-&gt;mBloodShop</c>).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetBloodMarkCatalog,
    ExpectedSize = 9, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GetBloodMarkCatalogRequest : IIncomingPacket<GetBloodMarkCatalogRequest>
{
}
