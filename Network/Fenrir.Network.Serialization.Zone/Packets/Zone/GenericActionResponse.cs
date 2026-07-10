using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Data is 130 bytes, not 100, because EU33 builds with USE_ITEM_LINK_V2 on.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GenericAction, ExpectedSize = 143)]
public readonly partial record struct GenericActionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(130)] public required byte[] Data { get; init; }
    public required int RuneValue { get; init; }
}
