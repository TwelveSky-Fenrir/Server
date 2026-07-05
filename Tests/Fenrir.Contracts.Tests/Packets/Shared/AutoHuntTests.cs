using System.Buffers.Binary;
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

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[AutoHunt.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4 + 5 * 4, 4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(68, 4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(72 + 2 * 4, 4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(108, 4), 5);

        var ok = AutoHunt.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.BuffType);
        Assert.Equal(2, packet.BuffStore[5]);
        Assert.Equal(3, packet.HuntType);
        Assert.Equal(4, packet.AttackType[2]);
        Assert.Equal(5, packet.AnimalFoodCmd);
    }
}
