using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Cluster.Wire.Packets;

/// <summary>
/// Enveloppe de bus d'events monde (op33) <b>relayée par le CenterServer aux Zones</b> (fan-out) — même layout
/// que <see cref="WorldEventInbound"/> : en-tête d'opcode 1 octet + <see cref="Sort"/> (4) + <see cref="Data"/>
/// (130) = 135 octets. Émise par le Center après avoir appliqué l'effet d'état monde (sauf exceptions 9998
/// hero-rank / 10000 close-proxy qui ne font pas de fan-out — Lot F4/E2).
/// </summary>
[FenrirPacket(FenrirServer.Center, FenrirDirection.Outgoing, Opcodes.Center.Outgoing.WorldEvent, ExpectedSize = 135)]
public readonly partial record struct WorldEventOutbound : IOutgoingPacket
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
