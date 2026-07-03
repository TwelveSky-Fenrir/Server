using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_UP_LEVEL_ITEM_RECV (ZONE.h:1352-1357, 29-byte payload): Result/Value[6]/Padding(1 dead byte on
///     the wire, pack(1)). <see cref="Padding" /> is intentionally set to a non-zero value here to prove
///     the trailing byte truly round-trips through the wire and is not silently dropped by the generator.
/// </summary>
public class ZcUpLevelItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(29, ZcUpLevelItemRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.UpLevelItemRecv, ZcUpLevelItemRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[ZcUpLevelItemRecv.PayloadSize];
        value.Write(actual);

        var expected = new byte[29];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcUpLevelItemRecv CreatePopulated()
    {
        return new ZcUpLevelItemRecv
        {
            Result = 11,
            Value = [100, 101, 102, 103, 104, 105],
            Padding = 0xAB
        };
    }

    private static void EncodeGolden(Span<byte> destination, ZcUpLevelItemRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Value[i]);
        destination[28] = value.Padding;
    }
}
