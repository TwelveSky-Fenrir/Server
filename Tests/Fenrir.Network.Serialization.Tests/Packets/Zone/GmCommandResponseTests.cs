using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGmCommandInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(104, GmCommandResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GmCommand, GmCommandResponse.Opcode);
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

    private static GmCommandResponse CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GmCommandResponse
        {
            Sort = v.NextInt(),
            GmData = v.NextByteArray(100)
        };
    }

    private static void EncodeGolden(Span<byte> destination, GmCommandResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Sort);
        value.GmData.CopyTo(destination[4..]);
    }
}
