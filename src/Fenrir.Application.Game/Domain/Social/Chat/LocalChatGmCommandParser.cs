using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Social.Chat;

public static class LocalChatGmCommandParser
{
    private const string YgDropName = "?ygdrop";
    private const string LabName = "?lab";
    private const string BossPrefix = "boss ";

    public static bool TryParse(string content, out LocalChatGmCommand command)
    {
        if (string.Equals(content, "where", StringComparison.Ordinal))
        {
            command = new LocalChatGmCommand
                { Kind = LocalChatGmCommandKind.Where, RequiredTier = GmCommandTier.Basic };
            return true;
        }

        if (TryMatchWholeWord(content, YgDropName, out var ygDropArgument))
        {
            command = new LocalChatGmCommand
            {
                Kind = LocalChatGmCommandKind.YgDrop,
                RequiredTier = GmCommandTier.Elevated,
                Argument = ygDropArgument
            };
            return true;
        }

        if (TryMatchWholeWord(content, LabName, out var labArgument))
        {
            command = new LocalChatGmCommand
            {
                Kind = LocalChatGmCommandKind.Lab,
                RequiredTier = GmCommandTier.Elevated,
                Argument = labArgument
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

    private static bool TryMatchWholeWord(string content, string name, out string? argument)
    {
        argument = null;

        if (!content.StartsWith(name, StringComparison.Ordinal))
            return false;

        if (content.Length == name.Length)
            return true;

        if (content[name.Length] != ' ')
            return false;

        argument = TrimmedArgumentAfter(content, name.Length + 1);
        return true;
    }

    private static string? TrimmedArgumentAfter(string content, int prefixLength)
    {
        if (content.Length <= prefixLength)
            return null;

        var remainder = content[prefixLength..].Trim();
        return remainder.Length == 0 ? null : remainder;
    }
}
