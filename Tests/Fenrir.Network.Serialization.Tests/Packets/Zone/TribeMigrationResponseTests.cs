using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcChangeToTribe4RecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, TribeMigrationResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeMigration, TribeMigrationResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new TribeMigrationResponse { Result = 1 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);

        Assert.Equal(expected, actual);
    }
}
