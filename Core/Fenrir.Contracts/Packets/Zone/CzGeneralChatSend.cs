using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GENERAL_CHAT_SEND (CLIENT.h:187-193, <c>S_GENERAL_CHAT_SEND</c>) — local chat. Handler l.7762:
///     empty content ⇒ <c>Quit()</c> (anti-fuzzing); filtered by <c>CheckChat(NORMAL_CHAT)</c> and mute
///     status. This fork intercepts GM commands (<c>where</c>/<c>ygdrop</c>/<c>lab</c>/<c>boss</c>) in the
///     text when <c>uUserSort &gt; 0</c>. Otherwise rebroadcasts as ZC 41 in AOI, filtered by sender's
///     tribe/alliance (the sender receives its own echo).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GeneralChatSend, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzGeneralChatSend : IIncomingPacket<CzGeneralChatSend>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
