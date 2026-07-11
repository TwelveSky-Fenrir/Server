using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     The horizontal (X/Z-only) fling vector applied to a monster's corpse at the instant a killing blow lands --
///     legacy repurposes the monster's own "destination" movement field to carry this vector for the duration of
///     the corpse-countdown windup, rather than an absolute world point (see <see cref="MonsterDeathSequence" />).
/// </summary>
/// <param name="X">Horizontal X component. Zero for every suppressed case -- see <see cref="MonsterDeathKnockback.Compute" />.</param>
/// <param name="Z">Horizontal Z component. Vertical (Y) is never modeled -- legacy forces it flat.</param>
public readonly record struct MonsterKnockbackVector(float X, float Z)
{
    public static readonly MonsterKnockbackVector Zero = new(0f, 0f);

    public bool IsZero => X == 0f && Z == 0f;
}

/// <summary>
///     Computes the knockback vector a monster's corpse is flung along at the instant its life reaches zero from
///     an avatar's killing blow -- behavior contract <c>wave12/A3-death-sequence.md</c>, side effect 1.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>Server/ts25zone/S07_MyGame02.cpp:3092-3126</c> (direction, the damage-type-gated zero
///     branch, the point-blank zero floor, and the four-way random magnitude draw with the critical-hit
///     override). Never call this on a live (not-yet-dead) monster's own combat resolution path other than the
///     avatar-kills-monster one -- the contract this ports is scoped to that path exclusively (never
///     monster-vs-monster or monster-vs-avatar).
/// </remarks>
public static class MonsterDeathKnockback
{
    /// <summary>
    ///     Legacy <c>MONSTER_INFO::mDamageType</c> value that marks a stationary/structure monster (e.g. the
    ///     tribe-symbol "Holy Stone" guardians) -- the SAME excluded value <c>Zone.TryApplyPvmFlinch</c> already
    ///     uses to exempt these monsters from the A009 hit-stagger roll (<c>Zone.Monsters.cs</c>,
    ///     <c>monster.Template.DamageType == 1</c>). The death-sequence contract's own Edge Cases section
    ///     explicitly ties the knockback-zero gate to "a related but separate check earlier in the same
    ///     combat-hit routine" -- i.e. the same flinch-exemption check, and therefore the same recovered value --
    ///     rather than an independently-unrecoverable second constant.
    /// </summary>
    public const byte StationaryDamageType = 1;

    /// <summary>
    ///     Killer/monster separation (in world units) at or below which the knockback is forced to zero --
    ///     avoids normalizing a near-zero-length direction vector on a point-blank kill
    ///     (<c>S07_MyGame02.cpp:3097-3101</c>).
    /// </summary>
    public const float PointBlankDistanceThreshold = 1f;

    /// <summary>
    ///     The "short, unit-length" knockback magnitude -- unconditional on a critical killing blow, and one of
    ///     two equally-likely (50% combined) outcomes of the non-critical four-way draw.
    /// </summary>
    public const float ShortMagnitude = 1f;

    /// <summary>One of the two "long fling" non-critical outcomes (25% of non-critical kills) -- roughly 4x <see cref="ShortMagnitude" />.</summary>
    public const float LongMagnitudeA = 4f;

    /// <summary>The other "long fling" non-critical outcome (25% of non-critical kills) -- roughly 3.7x <see cref="ShortMagnitude" />.</summary>
    public const float LongMagnitudeB = 3.7f;

    /// <summary>
    ///     Computes the horizontal knockback vector for one killing blow. Pure -- no I/O, no <see cref="MonsterEntity" />
    ///     mutation; see <see cref="MonsterDeathSequence.BeginCorpseCountdown" /> for the caller that applies the
    ///     result.
    /// </summary>
    /// <param name="killerX">Killer's world X at the moment of the killing blow.</param>
    /// <param name="killerZ">Killer's world Z at the moment of the killing blow.</param>
    /// <param name="monsterX">Dying monster's own world X.</param>
    /// <param name="monsterZ">Dying monster's own world Z.</param>
    /// <param name="monsterDamageType">The dying monster's static <c>DamageType</c> classification.</param>
    /// <param name="isCriticalHit">Whether the killing blow was itself a critical hit (resolved earlier in the same combat-hit processing).</param>
    /// <param name="random">
    ///     Draws the four-way magnitude bucket on a non-critical kill only -- a critical kill consumes no draw at
    ///     all, matching the contract's own "with no randomness involved" wording (preserves draw-sequence parity
    ///     with the rest of this zone's combat resolution, same posture as <c>Zone.TryApplyPvmFlinch</c>).
    /// </param>
    public static MonsterKnockbackVector Compute(float killerX, float killerZ, float monsterX, float monsterZ,
        byte monsterDamageType, bool isCriticalHit, IRandomSource random)
    {
        // Stationary/structure monsters never fling, regardless of distance or crit status (S07_MyGame02.cpp:3092,3122-3126).
        if (monsterDamageType == StationaryDamageType)
            return MonsterKnockbackVector.Zero;

        var dx = monsterX - killerX;
        var dz = monsterZ - killerZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);

        // Point-blank kill: no fling, and avoids normalizing a near-zero-length vector (S07_MyGame02.cpp:3097-3101).
        if (distance <= PointBlankDistanceThreshold)
            return MonsterKnockbackVector.Zero;

        var unitX = dx / distance;
        var unitZ = dz / distance;

        var magnitude = ResolveMagnitude(isCriticalHit, random);
        return new MonsterKnockbackVector(unitX * magnitude, unitZ * magnitude);
    }

    /// <summary>
    ///     A critical kill is unconditionally short -- no draw. A non-critical kill draws uniformly over 4
    ///     equally-likely outcomes: 2 of 4 (50%) resolve short, 1 of 4 (25%) resolves to <see cref="LongMagnitudeA" />,
    ///     1 of 4 (25%) resolves to <see cref="LongMagnitudeB" /> (<c>S07_MyGame02.cpp:3102-3120</c>). Net effect,
    ///     verified against the contract: a critical killing blow never produces a larger fling than a
    ///     non-critical one -- exactly half of all non-critical kills get a substantially larger knockback than
    ///     any critical kill ever does.
    /// </summary>
    private static float ResolveMagnitude(bool isCriticalHit, IRandomSource random)
    {
        if (isCriticalHit)
            return ShortMagnitude;

        return random.NextInt32(4) switch
        {
            0 or 1 => ShortMagnitude,
            2 => LongMagnitudeA,
            _ => LongMagnitudeB
        };
    }
}
