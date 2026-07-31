using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildAction, ExpectedSize = 1397)]
public readonly partial record struct GuildActionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    public required GuildInfo GuildInfo { get; init; }
}
