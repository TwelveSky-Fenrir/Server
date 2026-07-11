using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Social.Chat;

public enum ChatBroadcastKind : byte
{
    Local,

    Shout,

    Tribe
}

public readonly struct ChatZoneCommand
{
    public required int SenderCharacterId { get; init; }
    public required ChatBroadcastKind Kind { get; init; }
    public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
