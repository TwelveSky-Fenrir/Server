namespace Fenrir.Application.Game.Guilds;

/// <summary>
///     Posted by <c>GuildActionHandler</c> (and the guild-invite finalize path) AFTER a guild membership
///     change is already durably persisted (create/join/leave/kick/promote/demote/title/transfer/disband),
///     to mirror it onto ONE character's OWN hosting zone -- same cross-thread posture as
///     <see cref="Social.Mentor.MentorZoneCommand" /> (that type's own remarks explain why a direct field
///     write from a DIFFERENT character's request thread would be a single-writer-invariant violation this
///     closes: the target may be hosted by an entirely different <see cref="World.Zone" />/tick thread than
///     the actor whose request triggered the change).
/// </summary>
/// <param name="CharacterId">Whose <see cref="World.PlayerRuntimeState" /> this mirrors onto -- a no-op if they already left this zone.</param>
/// <param name="GuildId">Null = "no longer in a guild" (also clears <paramref name="GuildName" />/<paramref name="GuildRoleDb" />/<paramref name="GuildCallName" />).</param>
/// <param name="GuildName">The guild's display name -- "" when <paramref name="GuildId" /> is null.</param>
/// <param name="GuildRoleDb">DB-side role enum (0 member, 1 sub-master, 2 master) -- see <see cref="Social.GuildRoleCodec" />.</param>
/// <param name="GuildCallName">Cosmetic in-guild title (GuildMembers.CallName) -- "" when none set or when leaving.</param>
/// <param name="Applied">
///     Completed by <see cref="World.Zone.ApplyGuildMembershipCommand" /> the instant the tick actually
///     mirrors this command (whether it finds the player still present or not) -- same
///     "wait for the ACTUAL mirror, not merely the post" contract as <see cref="Inventory.InventoryZoneCommand.Applied" />.
///     Null for callers that only need the DB write's own durability (e.g. mirroring a guild's OTHER,
///     possibly-offline members after a kick/disband, where there is no requester-held
///     <see cref="World.PlayerRuntimeState.EconomyActionLock" /> waiting on this specific mirror).
/// </param>
public readonly record struct GuildMembershipZoneCommand(
    int CharacterId,
    int? GuildId,
    string GuildName,
    byte GuildRoleDb,
    string GuildCallName,
    TaskCompletionSource? Applied = null);
