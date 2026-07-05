using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>
///     ZC_UP_LEVEL_ITEM_RECV (ZONE.h:1352-1357): Padding is a real dead byte on the wire (pack(1)); set non-zero here
///     to prove it round-trips.
/// </summary>
public class ZcUpLevelItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(29, UpgradeCapeResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.UpgradeCape, UpgradeCapeResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[UpgradeCapeResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[29];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static UpgradeCapeResponse CreatePopulated()
    {
        return new UpgradeCapeResponse
        {
            Result = 11,
            Value = [100, 101, 102, 103, 104, 105],
            Padding = 0xAB
        };
    }

    private static void EncodeGolden(Span<byte> destination, UpgradeCapeResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Value[i]);
        destination[28] = value.Padding;
    }
}
