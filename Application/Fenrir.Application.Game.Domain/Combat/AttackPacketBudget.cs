using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Companion check to <see cref="CharacterMotionWhitelist" /> -- <c>CheckAttackPacket</c>.
///     Enforces, for one incoming attack-resolution sub-packet, the ceiling and replay guard the whitelist
///     last recorded against the character's current avatar action.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:1718-1761 (<c>CheckAttackPacket</c>: counter
///     increment/ceiling comparison 1741-1747, then replay-guard field comparison against the current action
///     category 1748-1751, both gated on the enforcement flag; failure is a plain reject, never a
///     disconnect) ; 1763-1774/1776-1823/3737-3740 (ProcessAttack01/02/06 call with counting enabled, the
///     default parameter) ; 1825-1838 (ProcessAttack03 also calls with counting enabled) ; 3527
///     (ProcessAttack05 calls with counting explicitly disabled) ; Server/ts25zone/H07_MyGame.h:497
///     (declaration, confirming the counting parameter's default) ; Server/Header/Protocol/STRUCT.h:958-978
///     (<c>ATTACK_FOR_PROTOCOL.mAttackActionValue4</c>, the replay-guard field).
///     Wired into the four live mCase 1/2/3/6 combat-resolution call sites (<c>Zone.Duel.cs</c>'s
///     <c>ApplyDuelAttack</c>, <c>Zone.Combat.cs</c>'s <c>ApplyCombatCommand</c>/<c>ApplyPvmAttack</c>, and
///     <c>Zone.Stun.cs</c>'s <c>ApplyUnstunAttack</c>), each with counting enabled (the default parameter),
///     matching every cited call site above except <c>ProcessAttack05</c>. mCase 5
///     (<see cref="Zone.ApplyStunAttack" />) deliberately never calls this -- see <c>ProcessAttack05</c>'s own
///     citation. Combat test fixtures across
///     <c>Fenrir.Application.Game.Tests</c> and the <c>Fenrir.IntegrationTests</c> bot client now establish a
///     non-zero <see cref="PlayerRuntimeState.AttackSubPacketCeiling" /> before posting a raw
///     <see cref="CombatCommand" />/wire attack packet, the way a real client always does.
/// </remarks>
public static class AttackPacketBudget
{
    /// <summary>
    ///     True if this attack-resolution sub-packet may proceed. False is a silent reject -- no disconnect,
    ///     no error response, no visible acknowledgment; a materially softer outcome than
    ///     <see cref="CharacterMotionWhitelist" />'s own disconnect sanction.
    /// </summary>
    /// <param name="state">
    ///     The attacking character's session state. <see cref="PlayerRuntimeState.AttackBudgetEnforced" />,
    ///     <see cref="PlayerRuntimeState.AttackSubPacketCeiling" />, and
    ///     <see cref="PlayerRuntimeState.ActionSort" /> were last set by
    ///     <see cref="CharacterMotionWhitelist" /> via the character's most recently accepted avatar
    ///     action; <see cref="PlayerRuntimeState.AttackSubPacketsUsed" /> is mutated here.
    /// </param>
    /// <param name="attackActionValue4">The incoming sub-packet's own replay-guard field.</param>
    /// <param name="countAttempt">
    ///     False skips the counter/ceiling comparison -- mirrors <c>ProcessAttack05</c>'s explicit opt-out.
    ///     Defaults to true, matching every other attack-resolution flow's default parameter. No current
    ///     Fenrir call site passes false: mCase 5 (stun, <see cref="Zone.ApplyStunAttack" />) is modeled but
    ///     deliberately never calls this method at all, reproducing <c>ProcessAttack05</c>'s fully-disabled
    ///     check without needing a <c>countAttempt: false</c> call site.
    /// </param>
    public static bool TryConsume(PlayerRuntimeState state, int attackActionValue4, bool countAttempt = true)
    {
        if (!state.AttackBudgetEnforced)
            return true;

        if (countAttempt)
        {
            state.AttackSubPacketsUsed++;
            if (state.AttackSubPacketsUsed > state.AttackSubPacketCeiling)
                return false;
        }

        return attackActionValue4 == state.ActionSort;
    }
}
