using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.Whisper, ExpectedSize = 107,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct WhisperRequest : IIncomingPacket<WhisperRequest>
{

        [FixedString(13)]
    public required string AvatarName { get; init; }

    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
