using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_TRIBE_CHAT_SEND — same typedef as CZ 38 (CLIENT.h:193). Local to the zone (no relay, unlike the
///     notice opcodes): handler l.11522 loops the zone's players and sends ZC 90 only to players of the
///     same tribe or an allied tribe (<c>ReturnAllianceTribe</c>). Empty content ⇒ <c>Quit()</c>; filtered
///     by <c>CheckChat(TRIBE_CHAT)</c> and mute. <see cref="Link" /> is transmitted as-is.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeChat, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeChatRequest : IIncomingPacket<TribeChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
