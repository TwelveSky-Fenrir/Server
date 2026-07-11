using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FishingCatch,
    ExpectedSize = 21)]
public readonly partial record struct FishingCatchResponse : IOutgoingPacket
{

        public required int Result { get; init; }

        public required int ItemIndex { get; init; }

    public required int Page { get; init; }
    public required int Index { get; init; }

        public required int XY { get; init; }
}
