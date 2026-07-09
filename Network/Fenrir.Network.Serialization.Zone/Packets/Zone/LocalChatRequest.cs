using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>
///     This fork intercepts GM commands (where/ygdrop/lab/boss/kill200/?clear) embedded in the chat text --
///     see <c>Fenrir.Application.Game.Domain.Social.Chat.LocalChatGmCommandParser</c> and
///     <c>Fenrir.Application.Game.Services.Chat.LocalChatService</c> for which of the six are fully
///     implemented (only "where") versus tier-gated but otherwise a logged no-op pending their own subsystem.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.LocalChat, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct LocalChatRequest : IIncomingPacket<LocalChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
