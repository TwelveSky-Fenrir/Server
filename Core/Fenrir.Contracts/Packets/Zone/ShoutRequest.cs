using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GENERAL_SHOUT_SEND — same typedef as CZ 38 (CLIENT.h:193). Registered unconditionally
///     (S04_MyWork01.cpp:42), but the handler (S04_MyWork02.cpp:8097) silently ignores the packet unless
///     <c>mServerNumber ∈ {37, 119, 124, 84}</c> — a runtime server-number condition, to be ported as
///     GameServer configuration. Handler l.8075: empty content ⇒ <c>Quit()</c>; filtered by
///     <c>CheckChat(GENERAL_CHAT)</c> and mute; no megaphone item is consumed in this fork. Broadcasts ZC
///     43 to the whole zone (<c>BroadcastServer(1)</c>), no tribe filter.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.Shout, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct ShoutRequest : IIncomingPacket<ShoutRequest>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
