using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FishingCatch,
    ExpectedSize = 21)]
public readonly partial record struct FishingCatchResponse : IOutgoingPacket
{
    /// <summary>1=item granted, 2=inventory full.</summary>
    public required int Result { get; init; }

    /// <summary>Item index of the fish/prize.</summary>
    public required int ItemIndex { get; init; }

    public required int Page { get; init; }
    public required int Index { get; init; }

    /// <summary>Legacy quirk: actually carries <c>tPosY</c>, not a packed X/Y pair.</summary>
    public required int XY { get; init; }
}
