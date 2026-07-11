using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Domain.Social.Chat;

public enum LocalChatGmCommandKind
{

        Where,

        YgDrop,

        Lab,

        Boss,

        Kill200,

        ClearInventory
}

public readonly record struct LocalChatGmCommand
{
    public required LocalChatGmCommandKind Kind { get; init; }
    public required GmCommandTier RequiredTier { get; init; }

        public string? Argument { get; init; }
}
