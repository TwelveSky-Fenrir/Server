using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcMakePetRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(28, CraftPetResponse.PayloadSize);
        Assert.Equal(4 + 6 * 4, CraftPetResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CraftPet, CraftPetResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var value = new[] { 92291, 0, 0, 15, 0, 0 };
        var packet = new CraftPetResponse { Result = 10000, Value = value };

        var golden = new byte[CraftPetResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 10000);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4 + i * 4), value[i]);

        Span<byte> buffer = new byte[CraftPetResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(CraftPetResponse.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
