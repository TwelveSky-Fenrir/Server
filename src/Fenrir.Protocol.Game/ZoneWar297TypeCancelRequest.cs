using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_297_TYPE_CANCEL_SEND CLIENT.h:492-497 ; mort en M33/LNW33 : opcode 100 jamais REGWORK1, hors table W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneWar297TypeCancel, ExpectedSize = 30,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ZoneWar297TypeCancelRequest : IIncomingPacket<ZoneWar297TypeCancelRequest>
{
    public required int ZoneNumber { get; init; }

    public required int Tribe { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
}
