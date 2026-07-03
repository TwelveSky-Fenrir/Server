using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_PARTY_CHAT_SEND — same typedef as CZ 38 (CLIENT.h:193). Handler l.9737: empty content ⇒
///     <c>Quit()</c>; no party ⇒ silent return; filtered by <c>CheckChat(PARTY_CHAT)</c>.
///     TRAP: <see cref="Link" /> is DEAD server-side — the handler never reads it and the relay struct
///     <c>RELAY_PARTY_CHAT_SEND</c> (STRUCT.h:1106-1111) does not transport it;
///     <c>
///         ProcessForRelay case
///         105
///     </c>
///     (S04_MyWork04.cpp:66-80) re-reads a zeroed <c>tLink</c> from the relay buffer, so the
///     resulting ZC 76 (GROUP lot) always carries a null link. Fenrir may decode this field (it is on the
///     wire) but MUST NOT propagate it. Relayed via center tSort=105 to every zone, filtered by exact
///     <c>aPartyName</c>.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PartyChat, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct PartyChatRequest : IIncomingPacket<PartyChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }

    /// <summary>On the wire, but dead server-side — do not propagate downstream (see type remarks).</summary>
    public required ItemLinkInfo Link { get; init; }
}
