using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTribeBankRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(212, TribeBankResponse.PayloadSize);
        Assert.Equal(4 + 4 + 200 + 4, TribeBankResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeBank, TribeBankResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var slots = new int[50];
        for (var i = 0; i < slots.Length; i++)
            slots[i] = (i + 1) * 1000;

        var packet = new TribeBankResponse { Result = 0, Sort = 2, TribeBankInfo = slots, Money = 555_000 };

        Span<byte> buffer = new byte[TribeBankResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(TribeBankResponse.PayloadSize, written);

        var golden = new byte[212];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 2);
        for (var i = 0; i < 50; i++)
            BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8 + i * 4), slots[i]);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(208), 555_000);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
