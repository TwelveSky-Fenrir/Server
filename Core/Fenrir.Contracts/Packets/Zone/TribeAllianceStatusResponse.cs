using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Sort: 1=open, 2=phase1 tick, 3=phase1 cancel, 4=phase2 tick, 5=phase2 cancel (Value=remaining time on ticks).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAllianceStatus, ExpectedSize = 9)]
public readonly partial record struct TribeAllianceStatusResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
