using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_START_PSHOP_SEND (CLIENT.h:306-310) — open a personal or deputy (proxy) shop stall.
///     <c>Sort</c> 1 = personal shop, 2 = proxy (offline deposit shop); any other value disconnects.
///     Runtime-gated behind <c>PPSHOP_V2</c> to <c>mServerNumber == 37</c> in EU33 (handler-side, not wire-visible).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.StartPshopSend, ExpectedSize = 1245,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzStartPshopSend : IIncomingPacket<CzStartPshopSend>
{
    public required int Sort { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
