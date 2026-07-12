namespace Fenrir.Tools.DbMigrator.Tests;

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
