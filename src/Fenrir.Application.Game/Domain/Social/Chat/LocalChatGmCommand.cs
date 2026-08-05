using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Gm;

namespace Fenrir.Application.Game.Domain.Social.Chat;

public enum LocalChatGmCommandKind
{
    Where,

    YgDrop,

    Lab,

    Boss,

    Kill200,

    ClearInventory,

    ReloadAll,

    ReloadMonsters,

    ReloadItems,

    ReloadQuests
}

public readonly record struct LocalChatGmCommand
{
    public required LocalChatGmCommandKind Kind { get; init; }
    public required GmCommandTier RequiredTier { get; init; }

    public string? Argument { get; init; }
}

public static class LocalChatGmCommandAudit
{
    public static short EventCodeFor(LocalChatGmCommandKind kind)
    {
        return kind switch
        {
            LocalChatGmCommandKind.Where => GmCommandCatalog.ChatCommandWhereAudit,
            LocalChatGmCommandKind.YgDrop => GmCommandCatalog.ChatCommandYgDropAudit,
            LocalChatGmCommandKind.Boss => GmCommandCatalog.ChatCommandBossAudit,
            LocalChatGmCommandKind.Kill200 => GmCommandCatalog.ChatCommandKill200Audit,
            LocalChatGmCommandKind.Lab => GmCommandCatalog.ChatCommandLabAudit,
            LocalChatGmCommandKind.ClearInventory => GmCommandCatalog.ChatCommandClearInventoryAudit,
            LocalChatGmCommandKind.ReloadAll => GmCommandCatalog.ChatCommandReloadAllAudit,
            LocalChatGmCommandKind.ReloadMonsters => GmCommandCatalog.ChatCommandReloadMonstersAudit,
            LocalChatGmCommandKind.ReloadItems => GmCommandCatalog.ChatCommandReloadItemsAudit,
            LocalChatGmCommandKind.ReloadQuests => GmCommandCatalog.ChatCommandReloadQuestsAudit,
            _ => 0
        };
    }
}
