using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Sort echoes request: view has Money=0; deposit has Money=player's new gold (debit already applied).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeBank, ExpectedSize = 213)]
public readonly record struct TribeBankResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(50)] public required int[] TribeBankInfo { get; init; }
    public required int Money { get; init; }
}
