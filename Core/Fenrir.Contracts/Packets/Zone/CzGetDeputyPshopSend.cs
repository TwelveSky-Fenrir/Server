using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GET_DEPUTY_PSHOP_SEND (CLIENT.h:447-452) — fetch the caller's own deputy (proxy) shop via
///     <c>mProxySystem.Process(mIndex, -1, 0)</c>. Registered under <c>#ifndef DISABLE_PROXYSHOP</c> (on
///     in EU33) on server numbers {1, 6, 11, 140, 37}, but the handler re-checks
///     <c>IsValidZoneForProxyShop</c>, which under <c>PPSHOP_V2</c> (active) collapses to
///     <c>mServerNumber == 37</c> — the proxy shop only actually works on zone 37 in EU33; the
///     registration on 1/6/11/140 is a disconnect trap. <c>UniqueNumber</c> is a plain <c>int</c> here
///     (not <c>DWORD</c>, unlike CZ_BUY_PSHOP_SEND). Reply: ZC_GET_DEPUTY_PSHOP_RECV.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetDeputyPshopSend,
    ExpectedSize = 30, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzGetDeputyPshopSend : IIncomingPacket<CzGetDeputyPshopSend>
{
    public required int Sort { get; init; }
    public required int UniqueNumber { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
