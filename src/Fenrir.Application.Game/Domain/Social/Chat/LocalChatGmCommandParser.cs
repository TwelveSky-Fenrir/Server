using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Social.Chat;

public static class LocalChatGmCommandParser
{
    private const string YgDropPrefix = "ygdrop";
    private const string LabPrefix = "lab";
    private const string BossPrefix = "boss ";

    public static bool TryParse(string content, out LocalChatGmCommand command)
    {
        if (string.Equals(content, "where", StringComparison.Ordinal))
        {
            command = new LocalChatGmCommand
                { Kind = LocalChatGmCommandKind.Where, RequiredTier = GmCommandTier.Basic };
            return true;
        }

        if (content.StartsWith(YgDropPrefix, StringComparison.Ordinal))
        {
            command = new LocalChatGmCommand
            {
                Kind = LocalChatGmCommandKind.YgDrop,
                RequiredTier = GmCommandTier.Elevated,
                Argument = TrimmedArgumentAfter(content, YgDropPrefix.Length)
            };
            return true;
        }

        if (content.StartsWith(LabPrefix, StringComparison.Ordinal))
        {
            command = new LocalChatGmCommand
            {
                Kind = LocalChatGmCommandKind.Lab,
                RequiredTier = GmCommandTier.Elevated,
                Argument = TrimmedArgumentAfter(content, LabPrefix.Length)
            };
            return true;
        }

        if (content.StartsWith(BossPrefix, StringComparison.Ordinal))
        {
            command = new LocalChatGmCommand
            {
                Kind = LocalChatGmCommandKind.Boss,
                RequiredTier = GmCommandTier.Elevated,
                Argument = TrimmedArgumentAfter(content, BossPrefix.Length)
            };
            return true;
        }

        if (string.Equals(content, "kill200", StringComparison.Ordinal))
        {
            command = new LocalChatGmCommand
                { Kind = LocalChatGmCommandKind.Kill200, RequiredTier = GmCommandTier.Basic };
            return true;
        }

        if (string.Equals(content, "?clear", StringComparison.Ordinal))
        {
            command = new LocalChatGmCommand
                { Kind = LocalChatGmCommandKind.ClearInventory, RequiredTier = GmCommandTier.Basic };
            return true;
        }

        command = default;
        return false;
    }

    private static string? TrimmedArgumentAfter(string content, int prefixLength)
    {
        if (content.Length <= prefixLength)
            return null;

        var remainder = content[prefixLength..].Trim();
        return remainder.Length == 0 ? null : remainder;
    }
}
