using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     CZ_SKY_UP_ITEM_SEND (CLIENT.h:265-272, 16-byte payload): Page1/Index1/Page2/Index2 — same typedef
///     as <see cref="UpgradeCapeRequest" />.
/// </summary>
public class CzSkyUpItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(16, SkyUpgradeItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.SkyUpgradeItem, SkyUpgradeItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 33);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 44);

        Assert.True(SkyUpgradeItemRequest.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Page1);
        Assert.Equal(22, decoded.Index1);
        Assert.Equal(33, decoded.Page2);
        Assert.Equal(44, decoded.Index2);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(SkyUpgradeItemRequest.TryRead(new byte[15], out _));
    }
}
