using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="HolyShieldResolver" /> (base-slot-9 absorption + hard-clear) --
///     <c>DecreaseHolyShield</c>/<c>RemoveHolyShield</c> (<c>Server/ts25zone/S07_MyGame04.cpp:2689-2749</c>).
/// </summary>
public class HolyShieldResolverTests
{
    private static int[] EmptyBuff()
    {
        return new int[70]; // 35 slots x [value, duration]
    }

    [Fact]
    public void Absorb_NoShield_ReturnsZero_AndLeavesBufferUnchanged()
    {
        var buff = EmptyBuff();
        var absorbed = HolyShieldResolver.Absorb(buff, 500);

        Assert.Equal(0, absorbed);
        Assert.Equal(0, buff[HolyShieldResolver.BaseSlot * 2]);
    }

    [Fact]
    public void Absorb_ShieldLargerThanDamage_AbsorbsFullDamage_AndDecrementsShield()
    {
        var buff = EmptyBuff();
        buff[HolyShieldResolver.BaseSlot * 2] = 1000;
        buff[HolyShieldResolver.BaseSlot * 2 + 1] = 42; // some remaining duration

        var absorbed = HolyShieldResolver.Absorb(buff, 300);

        Assert.Equal(300, absorbed);
        Assert.Equal(700, buff[HolyShieldResolver.BaseSlot * 2]);
        Assert.Equal(42, buff[HolyShieldResolver.BaseSlot * 2 + 1]); // still active -> duration untouched
    }

    [Fact]
    public void Absorb_ShieldSmallerThanDamage_AbsorbsShieldValue_AndFullyClearsIt()
    {
        var buff = EmptyBuff();
        buff[HolyShieldResolver.BaseSlot * 2] = 100;
        buff[HolyShieldResolver.BaseSlot * 2 + 1] = 42;

        var absorbed = HolyShieldResolver.Absorb(buff, 160);

        Assert.Equal(100, absorbed); // only what the shield held
        Assert.Equal(0, buff[HolyShieldResolver.BaseSlot * 2]);
        Assert.Equal(0, buff[HolyShieldResolver.BaseSlot * 2 + 1]); // consumed -> duration cleared too
    }

    [Fact]
    public void Absorb_NonPositiveDamage_IsANoOp()
    {
        var buff = EmptyBuff();
        buff[HolyShieldResolver.BaseSlot * 2] = 1000;

        Assert.Equal(0, HolyShieldResolver.Absorb(buff, 0));
        Assert.Equal(1000, buff[HolyShieldResolver.BaseSlot * 2]);
    }

    [Fact]
    public void RemoveAll_ClearsBaseSlotValueAndDuration_AndReportsChange()
    {
        var buff = EmptyBuff();
        buff[HolyShieldResolver.BaseSlot * 2] = 500;
        buff[HolyShieldResolver.BaseSlot * 2 + 1] = 42;

        var changed = HolyShieldResolver.RemoveAll(buff);

        Assert.True(changed);
        Assert.Equal(0, buff[HolyShieldResolver.BaseSlot * 2]);
        Assert.Equal(0, buff[HolyShieldResolver.BaseSlot * 2 + 1]);
    }

    [Fact]
    public void RemoveAll_NothingActive_ReportsNoChange()
    {
        Assert.False(HolyShieldResolver.RemoveAll(EmptyBuff()));
    }
}
