using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Avatar-registration request, sent once
///     <see cref="Fenrir.Network.Serialization.Packets.Zone.ZoneHandshakeRequest" />
///     has already succeeded. Carries only an identity string, the avatar name, and the client's claimed
///     initial action -- there is no Tribe/Gender/Head/Face field on this request at all; those are loaded
///     server-side from the character's own authoritative record and never trusted from the client at this step.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/CLIENT.h:162-176 (field list: identity string, avatar name, action --
///     confirmed no tribe/gender/head/face field exists on this request) ; Server/ts25zone/S04_MyWork02.cpp:748-782
///     (rejected outright unless Action.Type is exactly 0 and Action.Sort is one of two legal values) ;
///     Server/ts25zone/S04_MyWork02.cpp:788-806 (the character's tribe/previous-tribe/gender/head/face are
///     copied unconditionally from the server-side record here, never from anything this request carries).
/// </remarks>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.EnterWorld,
    ExpectedSize = 381, AllowedStates = [(byte)ZoneSessionState.TicketConsumed])]
public readonly partial record struct EnterWorldRequest : IIncomingPacket<EnterWorldRequest>
{
    [FixedString(255)] public required string Id { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
    public required ActionInfo Action { get; init; }
}
