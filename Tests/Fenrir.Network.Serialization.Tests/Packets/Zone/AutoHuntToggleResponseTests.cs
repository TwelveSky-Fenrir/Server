using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcAutoConfigRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, AutoHuntToggleResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AutoHuntToggle, AutoHuntToggleResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new AutoHuntToggleResponse { ServerIndex = 3, UniqueNumber = 0xC0FFEEu, AutoState = 1 };

        var actual = new byte[12];
        value.Write(actual);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 3);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4), 0xC0FFEEu);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 1);

        Assert.Equal(expected, actual);
    }
}
