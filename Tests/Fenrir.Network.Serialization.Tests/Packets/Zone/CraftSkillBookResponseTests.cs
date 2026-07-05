using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>ZC_MAKE_SKILL_RECV (ZONE.h:537, 28-byte payload) — same typedef as <see cref="CraftItemResponse" />.</summary>
public class ZcMakeSkillRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(28, CraftSkillBookResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CraftSkillBook, CraftSkillBookResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[CraftSkillBookResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[28];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static CraftSkillBookResponse CreatePopulated()
    {
        return new CraftSkillBookResponse { Result = 11, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, CraftSkillBookResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Value[i]);
    }
}
