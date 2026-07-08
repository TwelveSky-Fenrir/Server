using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.GiftList,
    ExpectedSize = 85)]
public readonly record struct GiftListResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    // [10][2] flattened row-major: [page*2+0]=itemId, [page*2+1]=0 (GIFT_V2 off in EU33).
    [FixedArray(20)] public required int[] GiftItem { get; init; }
}
