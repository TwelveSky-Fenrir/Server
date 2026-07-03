using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

public class AutoHuntTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(112, AutoHunt.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var autoHunt = new AutoHunt
        {
            BuffType = v.NextInt(),
            BuffStore = v.NextIntArray(16),
            HuntType = v.NextInt(),
            AttackType = v.NextIntArray(4),
            MonNum = v.NextInt(),
            ItemType = v.NextInt(),
            InvenCmd = v.NextInt(),
            DeathCmd = v.NextInt(),
            AnimalPreyCmd = v.NextInt(),
            AnimalFoodCmd = v.NextInt()
        };

        var buffer = new byte[AutoHunt.WireSize];
        var written = autoHunt.Write(buffer);
        Assert.Equal(AutoHunt.WireSize, written);

        Assert.True(AutoHunt.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(autoHunt, roundTripped);
    }
}
