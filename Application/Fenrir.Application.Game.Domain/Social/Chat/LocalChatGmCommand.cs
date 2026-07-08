using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Domain.Social.Chat;

/// <summary>
///     Which (if any) of LocalChat's six embedded GM commands <see cref="LocalChatGmCommandParser" /> matched --
///     see that type's own remarks for the legacy citations, matched in this exact order.
/// </summary>
public enum LocalChatGmCommandKind
{
    /// <summary>Exact text "where" -- self-location echo (zone number, integer X/Y/Z).</summary>
    Where,

    /// <summary>Text beginning with "ygdrop" -- global yang/gok PvP item-drop event toggle/status.</summary>
    YgDrop,

    /// <summary>Text beginning with "lab" -- per-tribe Labyrinth ("ZONE175") battle-state toggle/status.</summary>
    Lab,

    /// <summary>Text beginning with "boss " -- spawn a monster by id plus a cluster-wide announcement.</summary>
    Boss,

    /// <summary>Exact text "kill200" -- reset the sender's tribe's zone-200 battle result, then echo as ordinary chat.</summary>
    Kill200,

    /// <summary>Exact text "?clear" -- wipe every slot of the sender's own inventory.</summary>
    ClearInventory
}

/// <summary>
///     One recognized GM command: which command, the <see cref="GmCommandTier" /> its own legacy case gates at,
///     and (for the three commands that take one) the raw sub-argument text. Parsing only -- carries no tier
///     enforcement and no side effect; see <see cref="LocalChatGmCommandParser" />.
/// </summary>
public readonly record struct LocalChatGmCommand
{
    public required LocalChatGmCommandKind Kind { get; init; }
    public required GmCommandTier RequiredTier { get; init; }

    /// <summary>
    ///     The trimmed remainder after the command word for <see cref="LocalChatGmCommandKind.YgDrop" />/
    ///     <see cref="LocalChatGmCommandKind.Lab" /> ("on"/"off"/"status"/anything else) and
    ///     <see cref="LocalChatGmCommandKind.Boss" /> (the raw monster-id text, not yet parsed as a number) --
    ///     null for every other kind, and null for YgDrop/Lab/Boss too when nothing followed the command word.
    /// </summary>
    public string? Argument { get; init; }
}
