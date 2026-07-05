using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildAction, ExpectedSize = 513,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildActionRequest : IIncomingPacket<GuildActionRequest>
{
    public required int Sort { get; init; }
    [FixedArray(500)] public required byte[] Data { get; init; }
}
