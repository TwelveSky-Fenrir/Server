namespace Fenrir.Application.Game.Domain.Guilds;

/// <summary>
///     Pure top-up step for one guild's active buff reserve (game.Guilds BuffTime/BuffTimeForDiff) -- the
///     producer-side counterpart to <see cref="GuildBuffDecay" />. Models legacy's
///     <c>MyDB::UpdateGuildBuffTime</c> (<c>Server/ts25extra/S08_MyDB.cpp:1151-1186</c>), reached from the
///     GUILD_WORK tSort=15 relay case (<c>Server/ts25extra/S04_MyWork02.cpp:793-818</c>). Fenrir's own
///     equivalent trigger is the Guild Scroll item-use path (world.Items 558/1211/8415,
///     <c>Services.ZoneLifecycle.UseInventoryItemService.ResolveGuildScrollAsync</c>) rather than a wire-relay
///     case, since a Fenrir shard resolves the item use in-process instead of relaying it to a separate
///     ts25extra executable.
/// </summary>
/// <remarks>
///     <para>
///         BuffTimeForDiff is an ABSOLUTE future-expiry baseline (a UTC tick timestamp the reserve counts down
///         to), never a moving decay checkpoint -- see <see cref="GuildBuffDecay" />'s own remarks for the
///         companion decay-side half of this same data model. This producer is the ONLY one that ever
///         advances BuffTimeForDiff away from its prior value; <see cref="GuildBuffDecay" /> only ever reads
///         it, and <c>IGuildActionService.SetGuildBuffAsync</c> (GUILD_WORK tSort=14, buff-type selection)
///         never touches it either.
///     </para>
///     <para>
///         Legacy parity, in order (<c>Server/ts25extra/S08_MyDB.cpp:1151-1186</c>):
///         1) read the guild's current BuffTime/BuffTimeForDiff;
///         2) if the stored baseline is already in the past relative to now, reset it to now before extending
///         it -- a fully-expired (or never-established, i.e. still at the schema default of zero) reserve does
///         not stack the newly-added minutes onto a stale/absent past timestamp, it restarts counting from
///         now;
///         3) add the requested minutes (converted to the same tick unit) onto that baseline -- this becomes
///         the new BuffTimeForDiff. A top-up while a buff is still active extends the existing future
///         baseline, it never resets the clock in that case;
///         4) recompute BuffTime as the whole-minute difference between the new baseline and now (same
///         computation <see cref="GuildBuffDecay" /> itself performs);
///         5) the caller persists both BuffTime and the new BuffTimeForDiff together, in one combined write
///         (<c>IGuildRepository.SetBuffAsync</c> already does this for every one of this codebase's three
///         BuffTime/BuffTimeForDiff writers).
///     </para>
///     <para>
///         BuffType/BuffState are never touched by this producer, matching the legacy producer's own column
///         list -- see GUILD_WORK tSort=14's separate <c>IGuildActionService.SetGuildBuffAsync</c> for buff-
///         type selection.
///     </para>
/// </remarks>
public static class GuildBuffTopUp
{
    public static Result Apply(GuildSummaryDto guild, int minutes, DateTime nowUtc)
    {
        var baseline = guild.BuffTimeForDiff < nowUtc.Ticks ? nowUtc.Ticks : guild.BuffTimeForDiff;
        var newBaseline = baseline + minutes * TimeSpan.TicksPerMinute;

        // Defensive floor only -- minutes is always a positive per-item constant in every live caller
        // (GuildScrollBuffMinutes), so newBaseline is always > nowUtc.Ticks in practice and this never
        // actually clamps; kept so a hypothetical zero/negative minutes value can never produce a negative
        // BuffTime instead of a clean no-op-shaped result.
        var remainingMinutes = (newBaseline - nowUtc.Ticks) / TimeSpan.TicksPerMinute;
        var flooredBuffTime = remainingMinutes < 0 ? 0 : (int)remainingMinutes;

        return new Result(guild.BuffType, guild.BuffState, flooredBuffTime, newBaseline);
    }

    public readonly record struct Result(int BuffType, int BuffState, int BuffTime, long BuffTimeForDiff);
}
