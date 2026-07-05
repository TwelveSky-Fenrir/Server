using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>CZ_MAKE_SKILL_SEND (CLIENT.h:289, 36-byte payload) — same typedef as <see cref="CraftItemRequest" />.</summary>
public class CzMakeSkillSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(36, CraftSkillBookRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.CraftSkillBook, CraftSkillBookRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[36];
        EncodeGolden(golden, value);

        Assert.True(CraftSkillBookRequest.TryRead(golden, out var decoded));
        Assert.Equal(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CraftSkillBookRequest.TryRead(new byte[35], out _));
    }

    private static CraftSkillBookRequest CreatePopulated()
    {
        return new CraftSkillBookRequest
        {
            Sort = 11,
            Page1 = 22,
            Index1 = 33,
            Page2 = 44,
            Index2 = 55,
            Page3 = 66,
            Index3 = 77,
            Page4 = 88,
            Index4 = 99
        };
    }

    private static void EncodeGolden(Span<byte> destination, CraftSkillBookRequest value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Sort);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Page1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Index1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.Page2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Index2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], value.Page3);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], value.Index3);
        BinaryPrimitives.WriteInt32LittleEndian(destination[28..], value.Page4);
        BinaryPrimitives.WriteInt32LittleEndian(destination[32..], value.Index4);
    }
}
