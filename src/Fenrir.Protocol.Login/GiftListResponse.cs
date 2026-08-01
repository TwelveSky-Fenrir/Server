using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.GiftList,
    ExpectedSize = 85)]
public readonly partial record struct GiftListResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(20)] public required int[] GiftItem { get; init; }
}
