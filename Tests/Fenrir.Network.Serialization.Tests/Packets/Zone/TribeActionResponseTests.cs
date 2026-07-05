using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTribeWorkRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(108, TribeActionResponse.PayloadSize);
        Assert.Equal(4 + 4 + 100, TribeActionResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeAction, TribeActionResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var data = new byte[100];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i + 5);

        var packet = new TribeActionResponse { Result = 0, Sort = 7, Data = data };

        Span<byte> buffer = new byte[TribeActionResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(TribeActionResponse.PayloadSize, written);

        var golden = new byte[108];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 7);
        data.AsSpan().CopyTo(golden.AsSpan(8));

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }

    [Fact]
    public void Write_EchoesClientDataVerbatim()
    {
        var echoed = new byte[100];
        new Random(42).NextBytes(echoed);

        var packet = new TribeActionResponse { Result = 0, Sort = 2, Data = echoed };

        Span<byte> buffer = new byte[TribeActionResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(echoed.AsSpan().SequenceEqual(buffer[8..]));
    }
}
