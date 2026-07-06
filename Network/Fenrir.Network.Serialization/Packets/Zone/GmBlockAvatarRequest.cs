using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Fenrir's dedicated GM-BLOCK command (legacy case 519 "[GM]-BLOCK", multiplexed in legacy through
///     PROCESS_DATA_SEND, Server/ts25zone/S04_MyWork04.cpp:1487-1515 via Server/ts25zone/S04_MyWork02.cpp:1990-2013 --
///     Fenrir does not reuse that "one packet, many sub-commands" shape). AvatarName is legacy's only input: the
///     name of the online avatar to permanently block. Silently disconnects the sender if it isn't GM
///     (<c>ZoneClientSession.IsGm</c>) -- see <see cref="GmBlockAvatarResponse" /> for the rest of the
///     (deliberately asymmetric) response shape.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GmBlockAvatar, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct GmBlockAvatarRequest : IIncomingPacket<GmBlockAvatarRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
