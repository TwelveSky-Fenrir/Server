using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcProcessDataRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // USE_ITEM_LINK_V2 is ON in EU33: MAX_BROADCAST_DATA_SIZE = 130 (not 100).
        Assert.Equal(142, GenericActionResponse.PayloadSize);
        Assert.Equal(4 + 4 + 130 + 4, GenericActionResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GenericAction, GenericActionResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var data = new byte[130];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i + 1);

        var packet = new GenericActionResponse { Result = 0, Sort = 7997, Data = data, RuneValue = 5 };

        Span<byte> buffer = new byte[GenericActionResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GenericActionResponse.PayloadSize, written);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(7997, BinaryPrimitives.ReadInt32LittleEndian(buffer[4..]));
        Assert.True(data.AsSpan().SequenceEqual(buffer.Slice(8, 130)));
        Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(buffer[138..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var data = new byte[130];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(255 - i);

        var packet = new GenericActionResponse { Result = 1, Sort = 3, Data = data, RuneValue = 9 };

        var golden = new byte[142];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 3);
        data.CopyTo(golden.AsSpan(8, 130));
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(138), 9);

        Span<byte> buffer = new byte[GenericActionResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
