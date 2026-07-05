using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Sort echoes request: view has Money=0; withdraw has Money=player's new gold (already applied).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeBank, ExpectedSize = 213)]
public readonly partial record struct TribeBankResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(50)] public required int[] TribeBankInfo { get; init; }
    public required int Money { get; init; }
}
