using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Guilds;

/// <summary>
///     The outcome of a single CZ_GUILD_WORK_SEND sub-command: either the actor's session should be aborted
///     (mirrors the legacy's own <c>DisconnectReason.Faulted</c> shape for a malformed/unauthorized request), or
///     a <see cref="GuildActionResponse" /> (Result/Sort/GuildInfo) should be sent back.
/// </summary>
public readonly record struct GuildActionResult(bool Abort, int Sort, int Result, GuildInfo GuildInfo)
{
    public static readonly GuildActionResult Aborted = new(true, 0, 0, default);

    public static GuildActionResult Success(int sort, GuildInfo info, int result = 0)
    {
        return new GuildActionResult(false, sort, result, info);
    }
}

/// <summary>
///     Business logic for every CZ_GUILD_WORK_SEND (opcode 75) sub-command -- see
///     <see cref="Handlers.Guilds.GuildActionHandler" /> for the dispatch/session-plumbing half.
/// </summary>
public interface IGuildActionService
{
    /// <summary>tSort 1 -- create a guild. Requires level &gt;=30, sufficient money, a non-empty name, and no existing guild.</summary>
    public ValueTask<GuildActionResult> CreateGuildAsync(GuildActionRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct);

    /// <summary>tSort 2 -- info/roster refresh; must already be in a guild.</summary>
    public ValueTask<GuildActionResult> GetGuildInfoAsync(PlayerRuntimeState state, CancellationToken ct);

    /// <summary>
    ///     tSort 3 -- invite finalize. Requires master/sub-master role and a pending accepted invite. Member cap =
    ///     Grade*10.
    /// </summary>
    public ValueTask<GuildActionResult> FinalizeInviteAsync(PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    /// <summary>tSort 4 -- voluntary exit. The master cannot leave (must transfer/disband instead).</summary>
    public ValueTask<GuildActionResult> ExitGuildAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    /// <summary>tSort 5 -- guild notice, master only.</summary>
    public ValueTask<GuildActionResult> UpdateGuildNoticeAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    /// <summary>tSort 6 -- disband, master only, requires exactly 1 remaining member.</summary>
    public ValueTask<GuildActionResult> DisbandGuildAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    /// <summary>tSort 7 -- grade upgrade, master only; member count must already be at cap, level/cost thresholds per grade.</summary>
    public ValueTask<GuildActionResult> UpgradeGuildAsync(PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    /// <summary>
    ///     tSort 8 -- kick, master only. Target resolved by name, independent of online state; the master cannot be
    ///     kicked.
    /// </summary>
    public ValueTask<GuildActionResult> KickMemberAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    /// <summary>
    ///     tSort 9 -- AGM promote (1)/demote (2), master only. Promote refused if already at
    ///     <see cref="GuildActionService.MaxSubMasters" /> or the target isn't a plain member; demote requires the target
    ///     currently be a sub-master.
    /// </summary>
    public ValueTask<GuildActionResult> SetAgmRoleAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    /// <summary>tSort 10 -- member title/CallName, master only. Target must be a member, not the master.</summary>
    public ValueTask<GuildActionResult> SetMemberTitleAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    /// <summary>
    ///     tSort 14 -- buff type choice, master/sub-master only. A plain member gets a clean tResult=4, not
    ///     an abort. Requires at least 1 minute of buff-time reserve (only ever recharged by tSort 15's
    ///     guild scrolls). The buff's actual gameplay stat effect is undocumented, so only the state machine
    ///     (choose a type, track remaining reserve) is implemented -- no stat bonus is invented. Activation
    ///     stamps BuffTimeForDiff to now: <see cref="GuildBuffDecayHost" /> reads that checkpoint to burn
    ///     the reserve down over real time, so it must restart from here, never from whatever stale value (or
    ///     0) the row already carried.
    /// </summary>
    public ValueTask<GuildActionResult> SetGuildBuffAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    /// <summary>
    ///     tSort 17 -- transfer leadership, master only. Target must be in the actor's own zone. Success
    ///     replies tResult=2, not 0 -- a verified legacy quirk the client expects.
    /// </summary>
    public ValueTask<GuildActionResult> TransferLeadershipAsync(GuildActionRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken ct);

    /// <summary>
    ///     tSort 1001 -- logo, master only. Always replies with an empty GuildInfo, even on success (matches
    ///     a legacy quirk). Deliberately stricter than legacy here: the legacy's own role check accidentally
    ///     also passes guildless characters (a zero-effect legacy accident); Fenrir correctly rejects them.
    /// </summary>
    public ValueTask<GuildActionResult> SetGuildLogoAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);
}
