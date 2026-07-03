using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_SECRET_CHAT_SEND (CLIENT.h:337-344, <c>S_SECRET_CHAT_SEND</c>) — whisper. Handler l.8011: empty
///     name or content ⇒ <c>Quit()</c>; whispering yourself ⇒ silent return; filtered by
///     <c>CheckChat(SECRET_CHAT)</c>. Target resolved via ts25playuser: offline/not-found ⇒ ZC 42
///     <c>Result=1</c> to the sender and stop; otherwise ZC 42 <c>Result=0</c> to the sender (+ target's
///     zone number) then relay center tSort=103, delivered as ZC 42 <c>Result=3</c> on the target's zone.
///     Cross-tribe gating (<c>Result=2</c>) is commented out in this fork (l.8040-8045) — inter-tribe
///     whispers pass through.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.SecretChatSend, ExpectedSize = 107,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzSecretChatSend : IIncomingPacket<CzSecretChatSend>
{
    /// <summary>Recipient's avatar name.</summary>
    [FixedString(13)]
    public required string AvatarName { get; init; }

    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
