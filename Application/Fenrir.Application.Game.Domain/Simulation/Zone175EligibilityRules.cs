using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The two per-player predicates the Zone175 mission uses, kept separate because they differ by exactly one
///     condition -- the source contract's own "wave-clear vs reward eligibility differ by one condition" edge
///     case (<c>Server/ts25zone/S07_MyGame01.cpp:8853-8869</c> vs <c>:8617-8636</c>).
/// </summary>
/// <remarks>
///     "Ready" (the player is fully in-world) is implicit here: Fenrir's <c>Zone._players</c> only ever holds a
///     fully-entered player (see <c>Zone.HandleEnter</c> and <c>Zone.SelectDamageBasedKillCredit</c>'s own
///     remarks on that collapse), so iterating <c>Zone.Players</c> already satisfies the legacy readiness check.
///     "Zone-transition" maps to <see cref="PlayerRuntimeState.IsMovingZone" /> (legacy <c>mMoveZoneResult</c>),
///     "hiding" maps to <see cref="PlayerRuntimeState.VisibleState" /> == 0 (legacy <c>IsHiding()</c>), and
///     "death" maps to <see cref="PlayerRuntimeState.IsDead" />.
///     <para>
///         Primitive overloads exist so the rules are unit-testable without constructing a full
///         <see cref="PlayerRuntimeState" /> (which requires a live session); the state-forwarding overloads are
///         the production call sites.
///     </para>
/// </remarks>
public static class Zone175EligibilityRules
{
    /// <summary>
    ///     Presence, as the wave-alive scan uses it: ready and not mid-zone-transition and not hiding. A
    ///     <em>dead but present</em> player counts as present here (the wave stays alive), unlike
    ///     <see cref="IsRewardEligible(bool,bool,bool)" />.
    /// </summary>
    public static bool IsPresent(bool isMovingZone, bool isHidden)
    {
        return !isMovingZone && !isHidden;
    }

    /// <inheritdoc cref="IsPresent(bool,bool)" />
    public static bool IsPresent(PlayerRuntimeState state)
    {
        return IsPresent(state.IsMovingZone, state.VisibleState == 0);
    }

    /// <summary>
    ///     Reward eligibility: <see cref="IsPresent(bool,bool)" /> AND not dead. This is the extra condition the
    ///     reward routine adds over the presence scan -- so a dead-but-present player can keep a wave running yet
    ///     receive no reward for its clear.
    /// </summary>
    public static bool IsRewardEligible(bool isMovingZone, bool isHidden, bool isDead)
    {
        return IsPresent(isMovingZone, isHidden) && !isDead;
    }

    /// <inheritdoc cref="IsRewardEligible(bool,bool,bool)" />
    public static bool IsRewardEligible(PlayerRuntimeState state)
    {
        return IsRewardEligible(state.IsMovingZone, state.VisibleState == 0, state.IsDead);
    }
}
