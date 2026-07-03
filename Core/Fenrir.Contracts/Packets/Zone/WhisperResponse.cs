using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_SECRET_CHAT_RECV (ZONE.h:603-613, <c>ZCS_SECRET_CHAT_RECV</c>) — whisper AND this fork's
///     system-message channel ("[-Server-]", "[-GM-]", AFK-checker: 25+ call sites with
///     <see cref="Result" />=3). Builder <c>B_SECRET_CHAT_RECV</c> (S05_MyTransfer.cpp:736, multi-buffer
///     <c>CreateZ</c>). The meaning of <see cref="AvatarName" /> depends on <see cref="Result" />: for
///     0/1 it names the whisper TARGET (echo to the sender); for 3 it names the SENDER (delivery to the
///     recipient / system message).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Whisper, ExpectedSize = 111)]
public readonly partial record struct WhisperResponse : IOutgoingPacket
{
    /// <summary>
    ///     0 = sender echo + target zone ok; 1 = target offline; 2 = cross-tribe refused (disabled in this fork); 3 =
    ///     delivered to recipient / GM-system message.
    /// </summary>
    public required int Result { get; init; }

    /// <summary>Target's zone number, only meaningful when <see cref="Result" /> = 0; otherwise 0.</summary>
    public required int ZoneNumber { get; init; }

    /// <summary>Result 0/1: TARGET's name (echo). Result 3: SENDER's name.</summary>
    [FixedString(13)]
    public required string AvatarName { get; init; }

    [FixedString(61)] public required string Content { get; init; }

    /// <summary><c>int</c> on the wire, populated from a <c>BYTE uAuthInfo.AuthType</c> (GM/premium; 0 by default).</summary>
    public required int AuthType { get; init; }

    /// <summary>Zeroed when the builder is called with NULL.</summary>
    public required ItemLinkInfo Link { get; init; }
}
