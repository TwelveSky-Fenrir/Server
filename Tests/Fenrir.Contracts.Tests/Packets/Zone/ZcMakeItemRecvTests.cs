using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_MAKE_ITEM_RECV (ZONE.h:533-537, 28-byte payload): Result/Value[6] — same typedef as
///     <see cref="ZcMakeSkillRecv" />.
/// </summary>
public class ZcMakeItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(28, ZcMakeItemRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MakeItemRecv, ZcMakeItemRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[ZcMakeItemRecv.PayloadSize];
        value.Write(actual);

        var expected = new byte[28];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcMakeItemRecv CreatePopulated()
    {
        return new ZcMakeItemRecv { Result = 11, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, ZcMakeItemRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Value[i]);
    }
}
