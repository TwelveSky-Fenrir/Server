using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_MAKE_ITEM2_RECV (ZONE.h:1357, 29-byte payload) — same typedef as <see cref="ZcUpLevelItemRecv" />
///     (164), including the dead trailing <see cref="ZcUpLevelItemRecv.Padding" /> byte.
/// </summary>
public class ZcMakeItem2RecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(29, ZcMakeItem2Recv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MakeItem2Recv, ZcMakeItem2Recv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[ZcMakeItem2Recv.PayloadSize];
        value.Write(actual);

        var expected = new byte[29];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcMakeItem2Recv CreatePopulated()
    {
        return new ZcMakeItem2Recv
        {
            Result = 11,
            Value = [100, 101, 102, 103, 104, 105],
            Padding = 0xAB
        };
    }

    private static void EncodeGolden(Span<byte> destination, ZcMakeItem2Recv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Value[i]);
        destination[28] = value.Padding;
    }
}
