using System.Buffers.Binary;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class BuffInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(280, BuffInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var buffInfo = new BuffInfo
        {
            Buff = v.NextIntArray(70)
        };

        var buffer = new byte[BuffInfo.WireSize];
        var written = buffInfo.Write(buffer);
        Assert.Equal(BuffInfo.WireSize, written);

        Assert.True(BuffInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(buffInfo, roundTripped);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[BuffInfo.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 111);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(35 * 4, 4), 222);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(69 * 4, 4), 333);

        var ok = BuffInfo.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(70, packet.Buff.Length);
        Assert.Equal(111, packet.Buff[0]);
        Assert.Equal(222, packet.Buff[35]);
        Assert.Equal(333, packet.Buff[69]);
    }
}
