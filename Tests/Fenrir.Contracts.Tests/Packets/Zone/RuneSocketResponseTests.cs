using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_RUNE_SYSTEM_RECV (ZONE.h:495-502, 20-byte payload): Result/Page/Index/ItemIndex/RuneIndex. NOTE:
///     field order differs from the CZ 157 request — verified independently here.
/// </summary>
public class ZcRuneSystemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, RuneSocketResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.RuneSocket, RuneSocketResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new RuneSocketResponse { Result = 11, Page = 22, Index = 33, ItemIndex = 44, RuneIndex = 55 };

        var actual = new byte[RuneSocketResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 11);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 33);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(12), 44);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16), 55);

        Assert.Equal(expected, actual);
    }
}
