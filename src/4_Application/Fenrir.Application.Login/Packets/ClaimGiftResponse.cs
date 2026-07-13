using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Login.Packets;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ClaimGift,
    ExpectedSize = 5)]
public readonly partial record struct ClaimGiftResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
