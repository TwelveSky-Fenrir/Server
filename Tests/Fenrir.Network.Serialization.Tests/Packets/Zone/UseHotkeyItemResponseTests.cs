using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcUseHotkeyItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, UseHotkeyItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.UseHotkeyItem, UseHotkeyItemResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new UseHotkeyItemResponse { Result = 11, Page = 22, Index = 33 };

        var actual = new byte[UseHotkeyItemResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 11);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 33);

        Assert.Equal(expected, actual);
    }
}
