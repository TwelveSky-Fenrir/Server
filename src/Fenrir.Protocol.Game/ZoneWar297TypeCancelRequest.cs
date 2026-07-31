using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneWar297TypeCancel, ExpectedSize = 30,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ZoneWar297TypeCancelRequest : IIncomingPacket<ZoneWar297TypeCancelRequest>
{
    public required int ZoneNumber { get; init; }

    public required int Tribe { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
}
