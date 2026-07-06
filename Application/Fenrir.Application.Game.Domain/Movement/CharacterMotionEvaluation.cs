namespace Fenrir.Application.Game.Domain.Movement;

/// <summary>
///     What a legal (Sort, Type) avatar-action pair resolves to, per <see cref="CharacterMotionWhitelist" />
///     -- the four values the caller stores as live per-character-session state, replacing whatever the
///     previous action left there.
/// </summary>
/// <param name="SkillCategoryCode">
///     0-3. A plain classification of the action, consumed by a separate, later skill/mana/hotkey-consistency
///     check that is not this evaluation's concern -- only codes 1 and 2 are actually matched by any further
///     validation in that later check; 0 and 3 fall through it entirely.
/// </param>
/// <param name="AttackBudgetEnforced">
///     Whether the caller should count and cap follow-up attack-resolution sub-packets for this action at
///     all. False means an unbounded number of attack-resolution sub-packets is accepted for the lifetime of
///     this one action, by explicit original intent (Sort 65/74 only) -- not omission.
/// </param>
/// <param name="AttackFamilyTag">
///     0-5. Names which family of attack-resolution sub-packet this action expects; recorded for reuse
///     elsewhere, not interpreted further by this evaluation.
/// </param>
/// <param name="AttackSubPacketCeiling">
///     The exact number of attack-resolution sub-packets permitted for this one action before a new avatar
///     action (and therefore a fresh evaluation of the whitelist) must arrive. Meaningless when
///     <paramref name="AttackBudgetEnforced" /> is false.
/// </param>
public readonly record struct CharacterMotionEvaluation(
    int SkillCategoryCode,
    bool AttackBudgetEnforced,
    int AttackFamilyTag,
    int AttackSubPacketCeiling);
