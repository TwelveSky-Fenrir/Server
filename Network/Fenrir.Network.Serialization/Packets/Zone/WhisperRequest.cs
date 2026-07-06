using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Whispering yourself is silently ignored; cross-tribe gating is disabled in this fork (inter-tribe whispers
///     pass through).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.Whisper, ExpectedSize = 107,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct WhisperRequest : IIncomingPacket<WhisperRequest>
{
    /// <summary>Recipient's avatar name.</summary>
    [FixedString(13)]
    public required string AvatarName { get; init; }

    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
