using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>This fork intercepts GM commands (where/ygdrop/lab/boss) embedded in the chat text.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.LocalChat, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct LocalChatRequest : IIncomingPacket<LocalChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
