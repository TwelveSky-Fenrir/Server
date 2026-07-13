using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Cluster.Wire;

namespace Fenrir.Cluster.Wire.Packets;

/// <summary>
/// Enveloppe de bus d'events monde (op33) <b>poussée par une Zone au CenterServer</b> — layout legacy
/// <c>{BYTE tProtocol; int tSort; BYTE tData[130]}</c>. En-tête d'opcode 1 octet + <see cref="Sort"/> (4) +
/// <see cref="Data"/> (130) = <b>135 octets</b> sur le fil. <see cref="Sort"/> est le 2ᵉ niveau de dispatch
/// (effet d'état monde puis fan-out — voir l'ingesteur, Lot F4/E2). Admis uniquement une fois le pair
/// authentifié.
/// </summary>
[FenrirPacket(FenrirServer.Center, FenrirDirection.Incoming, Opcodes.Center.Incoming.WorldEvent, ExpectedSize = 135,
    AllowedStates = [(byte)CenterSessionState.Authenticated])]
public readonly partial record struct WorldEventInbound : IIncomingPacket<WorldEventInbound>
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
