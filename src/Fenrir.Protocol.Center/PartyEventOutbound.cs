using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Center;

[FenrirPacket(FenrirServer.Center, FenrirDirection.Outgoing, 57, ExpectedSize = 135)]
public readonly partial record struct PartyEventOutbound : IOutgoingPacket
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
