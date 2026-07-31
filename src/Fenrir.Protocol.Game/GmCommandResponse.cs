using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GmCommand, ExpectedSize = 105)]
public readonly partial record struct GmCommandResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] GmData { get; init; }
}
