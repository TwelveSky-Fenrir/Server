using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcBroadcastInfoRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(134, ZoneEventInfoResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneEventInfo, ZoneEventInfoResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[134];
        value.Write(actual);

        var expected = new byte[134];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZoneEventInfoResponse CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ZoneEventInfoResponse
        {
            Sort = v.NextInt(),
            Data = v.NextByteArray(130)
        };
    }

    private static void EncodeGolden(Span<byte> destination, ZoneEventInfoResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Sort);
        value.Data.CopyTo(destination[4..]);
    }
}
