namespace Fenrir.Tools.LegacyDataImport.Tests;

/// <summary>
///     Covers <c>MonsterReader</c>'s Thunder Giant <c>AttackType</c> override
///     (<c>Load_Monster</c>, S15_MyShare.cpp:603-604): an unconditional overwrite of physical array slot 80
///     (zero-based), which in this data file's current layout coincides with the record self-reporting
///     Index 81 and name "Thunder Giant".
/// </summary>
public sealed class MonsterReaderPatchTests(MonsterReaderFixture fixture) : IClassFixture<MonsterReaderFixture>
{
    private const int ThunderGiantSlot = 80;

    [Fact]
    public void ThunderGiantOverride_ForcesAttackTypeToOne()
    {
        var patched = fixture.Patched[ThunderGiantSlot];

        Assert.Equal(81, patched.Index);
        Assert.Equal("Thunder Giant", patched.Name);
        Assert.Equal(1, patched.AttackType);
    }

    [Fact]
    public void ThunderGiantOverride_RawFileHadADifferentAttackType()
    {
        // Confirms the patch actually changes something rather than the raw file already matching by luck --
        // if this ever starts failing because the raw data changed to already be 1, the override becomes a
        // no-op and this assertion (not the production code) should be revisited.
        var raw = fixture.Raw[ThunderGiantSlot];

        Assert.Equal(81, raw.Index);
        Assert.Equal(2, raw.AttackType);
    }

    [Fact]
    public void ThunderGiantOverride_OnlyTouchesThePatchedSlot()
    {
        for (var slot = 0; slot < fixture.Raw.Count; slot++)
        {
            if (slot == ThunderGiantSlot)
                continue;

            Assert.Equal(fixture.Raw[slot].AttackType, fixture.Patched[slot].AttackType);
        }
    }
}
