using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

// Distinct C++ struct from ZC 29/30/31 despite the identical 32-byte layout.
public class ZcSkyUpItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, SkyUpgradeItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.SkyUpgradeItem, SkyUpgradeItemResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[SkyUpgradeItemResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[32];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static SkyUpgradeItemResponse CreatePopulated()
    {
        return new SkyUpgradeItemResponse { Result = 11, Cost = 22, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, SkyUpgradeItemResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Cost);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(8 + i * 4)..], value.Value[i]);
    }
}
