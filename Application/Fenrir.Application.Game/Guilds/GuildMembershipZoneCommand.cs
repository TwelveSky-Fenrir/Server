namespace Fenrir.Application.Game.Guilds;

/// <summary>
///     Posted by <c>GuildActionHandler</c> (and the guild-invite finalize path) AFTER a guild membership
///     change is already durably persisted, to mirror it onto ONE character's OWN hosting zone -- same
///     cross-thread posture as <see cref="Social.Mentor.MentorZoneCommand" />: a direct field write from a
///     different character's request thread would violate the single-writer invariant, since the target
///     may be hosted by a different <see cref="World.Zone" />/tick thread than the actor whose request
///     triggered the change.
/// </summary>
/// <param name="CharacterId">Whose <see cref="World.PlayerRuntimeState" /> this mirrors onto -- a no-op if they already left this zone.</param>
/// <param name="GuildId">Null = "no longer in a guild" (also clears <paramref name="GuildName" />/<paramref name="GuildRoleDb" />/<paramref name="GuildCallName" />).</param>
/// <param name="GuildName">The guild's display name -- "" when <paramref name="GuildId" /> is null.</param>
/// <param name="GuildRoleDb">DB-side role enum (0 member, 1 sub-master, 2 master) -- see <see cref="Social.GuildRoleCodec" />.</param>
/// <param name="GuildCallName">Cosmetic in-guild title (GuildMembers.CallName) -- "" when none set or when leaving.</param>
/// <param name="Applied">
///     Completed by <see cref="World.Zone.ApplyGuildMembershipCommand" /> once the tick actually mirrors
///     this command, whether or not the player is still present -- same "wait for the actual mirror, not
///     just the post" contract as <see cref="Inventory.InventoryZoneCommand.Applied" />. Null when the
///     caller only needs the DB write's durability (e.g. mirroring a guild's other, possibly-offline
///     members after a kick/disband, where no requester-held lock is waiting on this specific mirror).
/// </param>
public readonly record struct GuildMembershipZoneCommand(
    int CharacterId,
    int? GuildId,
    string GuildName,
    byte GuildRoleDb,
    string GuildCallName,
    TaskCompletionSource? Applied = null);
