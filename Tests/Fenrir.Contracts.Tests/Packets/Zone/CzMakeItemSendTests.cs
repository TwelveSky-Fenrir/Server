using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     CZ_MAKE_ITEM_SEND (CLIENT.h:278-289, 36-byte payload): Sort + 4 (Page,Index) pairs — same typedef
///     as <see cref="CzMakeSkillSend" />/<see cref="CzMakePetSend" />/<see cref="CzMakeItem2Send" />.
/// </summary>
public class CzMakeItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(36, CzMakeItemSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.MakeItemSend, CzMakeItemSend.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[36];
        EncodeGolden(golden, value);

        Assert.True(CzMakeItemSend.TryRead(golden, out var decoded));
        Assert.Equal(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzMakeItemSend.TryRead(new byte[35], out _));
    }

    private static CzMakeItemSend CreatePopulated()
    {
        return new CzMakeItemSend
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

    private static void EncodeGolden(Span<byte> destination, CzMakeItemSend value)
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
