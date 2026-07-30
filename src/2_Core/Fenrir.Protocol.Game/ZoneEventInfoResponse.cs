using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneEventInfo,
    ExpectedSize = 135)]
public readonly partial record struct ZoneEventInfoResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(130)] public required byte[] Data { get; init; }
}
