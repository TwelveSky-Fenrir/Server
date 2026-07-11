using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="Zone195NokSanState" />: the process-wide Zone195 "Nok-San" stone state (per-slot
///     owner array + per-tribe stones-held counts), its atomic capture-flip commit
///     (Server/ts25zone/S07_MyGame01.cpp:8528-8577), and the combat-path monster-damage bonus consumer
///     (Server/ts25zone/S07_MyGame02.cpp:2326-2331).
/// </summary>
public class Zone195NokSanStateTests
{
    [Fact]
    public void FreshState_EverySlotUncaptured_EveryTribeHoldsNothing()
    {
        var state = new Zone195NokSanState();

        for (var slot = 0; slot < Zone195NokSanState.StoneSlotCount; slot++)
        {
            Assert.Equal(0, state.GetOwner(slot));
            Assert.Null(state.GetOwningTribe(slot));
        }

        for (byte tribe = 0; tribe < Zone195NokSanState.TribeCount; tribe++)
        {
            Assert.Equal(0, state.GetStonesHeld(tribe));
            Assert.Equal(0, state.GetMonsterDamageBonus(tribe));
        }
    }

    [Fact]
    public void CommitCapture_UncapturedSlot_CreditsOnlyCapturingTribe_NoPriorOwnerDebited()
    {
        var state = new Zone195NokSanState();

        state.CommitCapture(0, 1);

        // Owner encoding is tribe + 1.
        Assert.Equal(2, state.GetOwner(0));
        Assert.Equal((byte)1, state.GetOwningTribe(0));
        Assert.Equal(1, state.GetStonesHeld(1));

        // No other tribe was touched.
        Assert.Equal(0, state.GetStonesHeld(0));
        Assert.Equal(0, state.GetStonesHeld(2));
        Assert.Equal(0, state.GetStonesHeld(3));
    }

    [Fact]
    public void CommitCapture_Flip_DebitsPriorOwner_CreditsNewOwner()
    {
        var state = new Zone195NokSanState();
        state.CommitCapture(0, 1); // tribe 1 holds slot 0 (count 1)

        state.CommitCapture(0, 2); // tribe 2 flips slot 0

        Assert.Equal((byte)2, state.GetOwningTribe(0));
        Assert.Equal(0, state.GetStonesHeld(1)); // prior owner debited to zero
        Assert.Equal(1, state.GetStonesHeld(2)); // new owner credited
        Assert.Equal(3, state.GetOwner(0)); // tribe 2 + 1
    }

    [Fact]
    public void CommitCapture_CreditClampedAtFour()
    {
        var state = new Zone195NokSanState();

        // Five distinct slots captured by the same tribe -- the fifth credit must clamp at the max of 4.
        for (var slot = 0; slot < 5; slot++)
            state.CommitCapture(slot, 1);

        Assert.Equal(Zone195NokSanState.MaxStonesPerTribe, state.GetStonesHeld(1));
    }

    [Fact]
    public void GetMonsterDamageBonus_ScalesWithStonesHeld()
    {
        var state = new Zone195NokSanState();

        Assert.Equal(0, state.GetMonsterDamageBonus(1));

        state.CommitCapture(0, 1);
        Assert.Equal(Zone195NokSanState.MonsterDamageBonusPerStone, state.GetMonsterDamageBonus(1)); // 1 * 100

        state.CommitCapture(1, 1);
        Assert.Equal(2 * Zone195NokSanState.MonsterDamageBonusPerStone, state.GetMonsterDamageBonus(1)); // 2 * 100
    }

    [Fact]
    public void Snapshot_ReflectsCurrentOwnersAndCounts()
    {
        var state = new Zone195NokSanState();
        state.CommitCapture(0, 1);
        state.CommitCapture(4, 1);
        state.CommitCapture(8, 2);

        var snapshot = state.Snapshot();

        Assert.Equal(Zone195NokSanState.TribeCount, snapshot.StonesHeld.Length);
        Assert.Equal(Zone195NokSanState.StoneSlotCount, snapshot.Owners.Length);

        Assert.Equal(2, snapshot.StonesHeld[1]); // tribe 1 holds slots 0 and 4
        Assert.Equal(1, snapshot.StonesHeld[2]); // tribe 2 holds slot 8

        Assert.Equal(2, snapshot.Owners[0]); // tribe 1 + 1
        Assert.Equal(2, snapshot.Owners[4]); // tribe 1 + 1
        Assert.Equal(3, snapshot.Owners[8]); // tribe 2 + 1
        Assert.Equal(0, snapshot.Owners[1]); // uncaptured
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Zone195NokSanState.StoneSlotCount)]
    public void GetOwner_InvalidSlot_Throws(int slot)
    {
        var state = new Zone195NokSanState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetOwner(slot));
    }

    [Fact]
    public void CommitCapture_InvalidTribe_Throws()
    {
        var state = new Zone195NokSanState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.CommitCapture(0, (byte)Zone195NokSanState.TribeCount));
    }
}
