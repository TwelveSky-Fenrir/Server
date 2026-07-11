using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.GiftList,
    ExpectedSize = 85)]
public readonly partial record struct GiftListResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(20)] public required int[] GiftItem { get; init; }
}
