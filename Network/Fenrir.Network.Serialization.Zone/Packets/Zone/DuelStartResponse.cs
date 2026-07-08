using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelStart, ExpectedSize = 21)]
public readonly partial record struct DuelStartResponse : IOutgoingPacket
{
    [FixedArray(3)] public required int[] DuelState { get; init; }
    public required int RemainTime { get; init; }
    public required int EatDrugState { get; init; }
}
