namespace Fenrir.Application.Game.Guilds;

/// <summary>Posted after a guild membership change is already durably persisted, to mirror it onto one character's own hosting zone -- a direct field write here would violate the single-writer invariant, since the target may be on a different <see cref="World.Zone" />/tick thread.</summary>
/// <param name="CharacterId">Whose <see cref="World.PlayerRuntimeState" /> this mirrors onto -- a no-op if they already left this zone.</param>
/// <param name="GuildId">Null = "no longer in a guild" (also clears GuildName/GuildRoleDb/GuildCallName).</param>
/// <param name="GuildName">"" when <paramref name="GuildId" /> is null.</param>
/// <param name="GuildRoleDb">DB-side role enum (0 member, 1 sub-master, 2 master).</param>
/// <param name="GuildCallName">Cosmetic in-guild title -- "" when none set or when leaving.</param>
/// <param name="Applied">Completed once the tick actually mirrors this command. Null when the caller only needs the DB write's durability, not this specific mirror.</param>
public readonly record struct GuildMembershipZoneCommand(
    int CharacterId,
    int? GuildId,
    string GuildName,
    byte GuildRoleDb,
    string GuildCallName,
    TaskCompletionSource? Applied = null);
