using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildAction, ExpectedSize = 513,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildActionRequest : IIncomingPacket<GuildActionRequest>
{
    public required int Sort { get; init; }
    [FixedArray(500)] public required byte[] Data { get; init; }
}
