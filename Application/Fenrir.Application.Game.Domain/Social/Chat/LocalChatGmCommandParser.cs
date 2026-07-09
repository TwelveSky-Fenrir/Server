using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Domain.Social.Chat;

/// <summary>
///     Pure text-pattern recognizer for LocalChat's embedded GM command sub-parser -- string matching only, in
///     the exact order legacy's own <c>if</c>/<c>else if</c> chain checks them. Carries no tier enforcement and
///     no side effect: callers (<see cref="Fenrir.Application.Game.Abstractions.Chat.ILocalChatService" />) must
///     only call this once the sender is already known to hold any GM tier at all (legacy's outer
///     <c>uUserSort &gt; 0</c> guard around this whole switch), and must gate each match's own
///     <see cref="LocalChatGmCommand.RequiredTier" /> via <c>ZoneClientSession.MeetsGmTier</c> before acting on
///     it -- a non-GM sender must never reach this parser at all, matching legacy's own fall-through-to-ordinary-chat
///     behavior for that case.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:7798-7961 (full sub-command switch) ; :7800-7806 (exact
///     "where", tier &gt; 0) ; :7808-7849 (prefix "ygdrop", tier gate :7810 requires &gt;= 10) ; :7851-7903
///     (prefix "lab", tier gate :7853 requires &gt;= 10) ; :7906-7932 (prefix "boss ", tier gate :7908 requires
///     &gt;= 10, monster-id argument parsed from the remainder) ; :7933-7939 (exact "kill200", tier &gt; 0) ;
///     :7940-7961 (exact "?clear", tier &gt; 0). Only the six patterns and their tier gates are ported here --
///     the mechanics each command drives (yang/gok drop economy flag, Labyrinth/ZONE175 per-tribe state,
///     monster-spawn-by-id, the zone-200 per-tribe battle-result counter, a full-inventory wipe) are not modeled
///     by this parser; see <see cref="Fenrir.Application.Game.Services.Chat.LocalChatService" />'s own remarks
///     for which of those are implemented and which are a deliberate, logged no-op pending their own contract.
/// </remarks>
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
