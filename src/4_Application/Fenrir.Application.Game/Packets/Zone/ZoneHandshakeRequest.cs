using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneHandshake, ExpectedSize = 272,
    AllowedStates = [(byte)ZoneSessionState.Connected])]
public readonly partial record struct ZoneHandshakeRequest : IIncomingPacket<ZoneHandshakeRequest>
{
    [FixedString(255)] public required string Id { get; init; }
    public required int Tribe { get; init; }
    public required int UserSort { get; init; }
}
