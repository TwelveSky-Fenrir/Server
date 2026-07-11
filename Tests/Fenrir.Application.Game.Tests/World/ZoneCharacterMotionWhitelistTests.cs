using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <c>Zone.PlayerLifecycle</c>'s wiring of
///     <see cref="Fenrir.Application.Game.Domain.Movement.CharacterMotionWhitelist" /> into <c>HandleMove</c>:
///     disconnect on an illegal (Sort, Type) pair, and session-state population/reset on a legal one.
/// </summary>
/// <remarks>
///     Sorts 42/44 both resolve to <c>CharacterMotionWhitelist</c>'s skill-cast category (2), so any test
///     below using either now also passes through D2 hook 4 (<c>Zone.EvaluateSkillCastTamperGuard</c>) --
///     unrelated to what these two tests actually exercise (attack-budget side effects, not skill-cast
///     tamper detection). <see cref="Action" />'s default <c>skillNumber = 0</c>/<c>SkillGradeNum1 = 0</c>
///     means the guard's own <c>HotkeyMismatch</c> check needs a hotkey bound to (skill 0, grade 0) --
///     nonsensical as a real skill, but a faithful, minimal stand-in for "this whitelist-focused test's
///     caster is not exercising any particular skill identity."
/// </remarks>
public class ZoneCharacterMotionWhitelistTests
{
    private static void SeedSkillHotkey(PlayerRuntimeState state, int skillId, int investedGrade)
    {
        state.Hotkeys = state.Hotkeys.SetItem((0, 0), new HotkeySlot(HotkeyBindingKind.Skill, skillId,
            investedGrade));
    }

    private static ActionInfo Action(int sort, int type, float x = 10f, float z = 10f, int skillNumber = 0)
    {
        return new ActionInfo
        {
            Type = type,
            Sort = sort,
            Frame = 0,
            Location = [x, 0f, z],
            TargetLocation = [x, 0f, z],
            Front = 0f,
            TargetFront = 0f,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = skillNumber,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
    }

    [Fact]
    public void IllegalSortTypePair_DisconnectsSession_AndNeverUpdatesPosition()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Sort 8 has no row in the whitelist at all.
        zone.Post(ZoneCommand.Move(10, Action(8, 0, 50f, 50f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, mover.DisconnectReason);

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(10f, state!.PosX);
        Assert.Equal(10f, state.PosZ);
        Assert.Equal(0, state.ActionSort);
    }

    /// <summary>
    ///     Sort 31 is the dead <c>#ifdef __MOBILE__</c> sibling of Sort 30 -- confirmed illegal in every
    ///     shipped build, unlike Sort 30 itself.
    /// </summary>
    [Fact]
    public void Sort31_DeadMobileOnlySibling_DisconnectsSession()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, Action(31, 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, mover.DisconnectReason);
    }

    [Fact]
    public void NegativeSort_DisconnectsSession()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, Action(-1, 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, mover.DisconnectReason);
    }

    [Fact]
    public void TypeNotLegalForSort_DisconnectsSession()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Sort 42 only legalizes Type 3.
        zone.Post(ZoneCommand.Move(10, Action(42, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, mover.DisconnectReason);
    }

    [Fact]
    public void LegalAction_PopulatesAttackBudgetFields_FromTheWhitelist()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // D2 hook 4: Sort 44 resolves to the skill-cast tamper guard's category -- seed a matching (skill 0,
        // grade 0) hotkey (Action(44, 3)'s own skillNumber/SkillGradeNum1 defaults) so this whitelist-focused
        // test's caster passes HotkeyMismatch, since it isn't exercising any particular skill identity.
        Assert.True(zone.TryGetPlayer(10, out var seedState));
        SeedSkillHotkey(seedState!, 0, 0);

        // Sort 44, Type 3 -> enforced, tag 3, ceiling 5. Same position as spawn: MovementRules' speed gate is
        // not this test's concern, so the move is a trivially-plausible zero-distance one.
        zone.Post(ZoneCommand.Move(10, Action(44, 3)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.True(state!.AttackBudgetEnforced);
        Assert.Equal(3, state.AttackFamilyTag);
        Assert.Equal(5, state.AttackSubPacketCeiling);
        Assert.Equal(0, state.AttackSubPacketsUsed);
        Assert.Equal(44, state.ActionSort);
        Assert.Null(mover.DisconnectReason);
    }

    [Fact]
    public void EnforcementOffAction_ClearsTheEnforcementFlag()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Sort 65 explicitly clears the attack-budget enforcement flag -- unbounded sub-packets for its
        // lifetime. Same position as spawn: a trivially-plausible zero-distance move.
        zone.Post(ZoneCommand.Move(10, Action(65, 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.False(state!.AttackBudgetEnforced);
        Assert.Equal(5, state.AttackFamilyTag);
    }

    [Fact]
    public void EveryAcceptedAction_UnconditionallyReplacesThePreviousBudget_AndResetsTheUsedCounter()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // D2 hook 4: both Sort 44 and Sort 42 below resolve to the skill-cast tamper guard's category -- seed
        // a matching (skill 0, grade 0) hotkey once, covering both actions' shared skillNumber/SkillGradeNum1
        // defaults (see this class's own remarks).
        Assert.True(zone.TryGetPlayer(10, out var seedState));
        SeedSkillHotkey(seedState!, 0, 0);

        // First action: Sort 44 -> ceiling 5. Same position as spawn: a trivially-plausible zero-distance move.
        zone.Post(ZoneCommand.Move(10, Action(44, 3)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(5, state!.AttackSubPacketCeiling);

        // Simulate a partially-consumed budget on the first action (as AttackPacketBudget.TryConsume would).
        state.AttackSubPacketsUsed = 3;

        // Second, unrelated legal action: Sort 42 -> ceiling 1, even though the previous budget (5) wasn't
        // exhausted -- the new action unconditionally replaces it and starts a fresh used-counter at 0. Kept at
        // the same position again so plausibility never depends on real wall-clock timing between ticks.
        zone.Post(ZoneCommand.Move(10, Action(42, 3)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.AttackSubPacketCeiling);
        Assert.Equal(0, state.AttackSubPacketsUsed);
        Assert.Equal(42, state.ActionSort);
    }

    [Fact]
    public void ImplausibleMove_NeverAppliesWhitelistSideEffects_EvenThoughThePairIsLegal()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var ceilingBefore = state!.AttackSubPacketCeiling;

        // Legal pair (Sort 44 / Type 3), but a teleport-scale jump MovementRules rejects on speed alone.
        zone.Post(ZoneCommand.Move(10, Action(44, 3, 999_999f, 999_999f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Rejected for implausibility, not illegality -- no disconnect, but also no whitelist side effects,
        // matching ActionSort's own "never set by a rejected move" contract (ActionSort stays 0/idle here).
        Assert.Null(mover.DisconnectReason);
        Assert.Equal(0, state.ActionSort);
        Assert.Equal(ceilingBefore, state.AttackSubPacketCeiling);
    }

    [Fact]
    public void UnknownCharacter_MoveIsIgnored_NoWhitelistEvaluationAttempted()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.Post(ZoneCommand.Move(999, Action(8, 0))); // illegal pair, but character was never tracked
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(999, out _));
    }
}
