using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CancelHoisundo,
    ExpectedSize = 22)]
public readonly partial record struct CancelHoisundoResponse : IOutgoingPacket
{
    public required int ZoneNumber { get; init; }

    public required int HoisundoIndex { get; init; }

    [FixedString(13)] public required string HoisundoName { get; init; }
}
