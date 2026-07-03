using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGmCommandInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(104, ZcGmCommandInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GmCommandInfo, ZcGmCommandInfo.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[104];
        value.Write(actual);

        var expected = new byte[104];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcGmCommandInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ZcGmCommandInfo
        {
            Sort = v.NextInt(),
            GmData = v.NextByteArray(100)
        };
    }

    private static void EncodeGolden(Span<byte> destination, ZcGmCommandInfo value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Sort);
        value.GmData.CopyTo(destination[4..]);
    }
}
