namespace Fenrir.Application.Game.Domain.Guilds;

/// <summary>
///     Posted once per exhausted guild per decay pass -- same-shard, immediately, by
///     <c>Hosting.Guilds.GuildBuffDecayHost</c>, and cross-shard, on every OTHER live shard, by
///     <c>Hosting.GuildBuffExpiryRelayHost</c> after its own relay poll. See <see cref="World.Zone" />'s own
///     <c>ApplyGuildBuffExpiryCommand</c> for what this triggers.
/// </summary>
/// <param name="GuildId">
///     Matched against <see cref="World.PlayerRuntimeState.GuildId" /> -- not a guild-name string comparison
///     (see that consumer's own remarks for why: guild names are enforced unique at creation, so identity by
///     id is functionally equivalent to legacy's own name match and sidesteps an unverified string-comparison
///     semantic).
/// </param>
/// <param name="NewBuffTime">
///     The freshly decayed reserve value for this pass -- always below one unit (typically exactly zero,
///     floored) whenever this command is posted at all.
/// </param>
public readonly record struct GuildBuffExpiryZoneCommand(int GuildId, int NewBuffTime);
