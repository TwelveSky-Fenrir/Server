using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GmCommand, ExpectedSize = 105)]
public readonly partial record struct GmCommandResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] GmData { get; init; }
}
