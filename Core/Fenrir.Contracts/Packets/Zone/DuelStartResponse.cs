using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelStart, ExpectedSize = 21)]
public readonly partial record struct DuelStartResponse : IOutgoingPacket
{
    [FixedArray(3)] public required int[] DuelState { get; init; }
    public required int RemainTime { get; init; }
    public required int EatDrugState { get; init; }
}
