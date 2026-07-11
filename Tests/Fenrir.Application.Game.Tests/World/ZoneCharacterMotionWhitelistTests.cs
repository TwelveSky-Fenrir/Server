using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

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

        zone.Post(ZoneCommand.Move(10, Action(8, 0, 50f, 50f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, mover.DisconnectReason);

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(10f, state!.PosX);
        Assert.Equal(10f, state.PosZ);
        Assert.Equal(0, state.ActionSort);
    }

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

        Assert.True(zone.TryGetPlayer(10, out var seedState));
        SeedSkillHotkey(seedState!, 0, 0);

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

        Assert.True(zone.TryGetPlayer(10, out var seedState));
        SeedSkillHotkey(seedState!, 0, 0);

        zone.Post(ZoneCommand.Move(10, Action(44, 3)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(5, state!.AttackSubPacketCeiling);

        state.AttackSubPacketsUsed = 3;

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

        zone.Post(ZoneCommand.Move(10, Action(44, 3, 999_999f, 999_999f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(mover.DisconnectReason);
        Assert.Equal(0, state.ActionSort);
        Assert.Equal(ceilingBefore, state.AttackSubPacketCeiling);
    }

    [Fact]
    public void UnknownCharacter_MoveIsIgnored_NoWhitelistEvaluationAttempted()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.Post(ZoneCommand.Move(999, Action(8, 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(999, out _));
    }
}
