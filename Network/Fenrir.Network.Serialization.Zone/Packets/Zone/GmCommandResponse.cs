using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>GmData is 100 bytes (MAX_TRIBE_WORK_SIZE), not the 130-byte MAX_BROADCAST_DATA_SIZE used elsewhere.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GmCommand, ExpectedSize = 105)]
public readonly partial record struct GmCommandResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] GmData { get; init; }
}
