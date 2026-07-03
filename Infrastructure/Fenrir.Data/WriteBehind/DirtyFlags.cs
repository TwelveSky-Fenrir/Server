namespace Fenrir.Data.WriteBehind;

/// <summary>
///     Marks which slice of a player entity's state is stale relative to what's durably persisted in SQL
///     (architecture reference §10.5). The zone tick raises these flags; nothing else does.
/// </summary>
/// <remarks>
///     <see cref="Vitals" /> has no writer yet in M1 (life/mana persistence isn't wired up) -- it exists now
///     purely so this enum never needs a breaking rename once it is. <see cref="Progression" /> (added by
///     Phase C/V3 Combat) is in the exact same boat: Experience/ContributionPoints changes
///     (<c>Zone.GrantMonsterKillExperience</c>, the death XP-loss branch of <c>Zone.ApplyDeath</c>) mark this
///     flag today, but no flush callback reads it yet -- persistence for these fields is a separate,
///     not-yet-scheduled follow-up.
/// </remarks>
[Flags]
public enum DirtyFlags : byte
{
    None = 0,
    Position = 1 << 0,
    Vitals = 1 << 1,
    Progression = 1 << 2
}
