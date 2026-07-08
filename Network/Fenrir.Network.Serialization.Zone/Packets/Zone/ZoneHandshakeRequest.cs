using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>
///     CZ_TEMP_REGISTER_SEND -- the temporary-registration request, first packet after ZC_CONNECT_OK_RECV.
///     <see cref="Tribe" /> is used exactly once, to check the declared tribe's population bucket against this
///     zone/server's quota ceiling; on acceptance the connection's actually-recorded tribe is immediately
///     overwritten from the server-side account lookup, discarding this declared value (see
///     <c>Fenrir.Application.Game.Services.ZoneLifecycle.ZoneHandshakeService</c>).
///     <see cref="UserSort" /> is deserialized but never consulted anywhere downstream -- matches legacy, whose
///     own equivalent field (<c>tUserSort</c>) has zero read sites in the cited handler either.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/CLIENT.h:162-176 (field list: identity string, declared tribe, and the
///     unread additional field) ; Server/ts25zone/S04_MyWork02.cpp:596-695 (declared-tribe legal range and
///     quota-ceiling divisor, keyed per zone/server: 0-2/÷3 for a three-tribe zone, 0-3/÷4 for a four-tribe
///     zone) ; Server/ts25zone/S04_MyWork02.cpp:728-741 (post-acceptance tribe overwrite from the account lookup).
/// </remarks>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneHandshake, ExpectedSize = 272,
    AllowedStates = [(byte)ZoneSessionState.Connected])]
public readonly partial record struct ZoneHandshakeRequest : IIncomingPacket<ZoneHandshakeRequest>
{
    // XOR USE_XOR_UID: de-obfuscated by the handler/Network layer, not by TryRead here.
    [FixedString(255)] public required string Id { get; init; }
    public required int Tribe { get; init; }
    public required int UserSort { get; init; }
}
