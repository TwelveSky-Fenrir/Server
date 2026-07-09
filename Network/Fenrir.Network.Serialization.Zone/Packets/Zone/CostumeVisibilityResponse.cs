using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CostumeVisibility,
    ExpectedSize = 13)]
public readonly record struct CostumeVisibilityResponse : IOutgoingPacket
{
    /// <summary>Echo of CZ 139's <c>tSort</c> (0/1).</summary>
    public required int Sort { get; init; }

    /// <summary>Always 0 in the single call site.</summary>
    public required int Sort2 { get; init; }

    /// <summary>Always 0 in the single call site.</summary>
    public required int Sort3 { get; init; }
}
