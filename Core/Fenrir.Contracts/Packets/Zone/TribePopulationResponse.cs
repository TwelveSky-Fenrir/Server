using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GET_ZONE_CONNECT_USER_RECV (ZONE.h:1047-1051) — builder
///     <c>B_GET_ZONE_CONNECT_USER_RECV</c> (S05_MyTransfer.cpp:1375); reply to CZ 92, emitted 4 times in
///     a row (S04_MyWork02.cpp:12848-12858). <see cref="ZoneNumber" /> is REPURPOSED by this fork as a
///     tribe id (348 = Noble Dragon, 349 = Royal Serpent, 350 = Grand Tiger, 351 = Nangin), never a real
///     zone number.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribePopulation,
    ExpectedSize = 9)]
public readonly partial record struct TribePopulationResponse : IOutgoingPacket
{
    public required int ZoneNumber { get; init; }
    public required int ConnectedUser { get; init; }
}
