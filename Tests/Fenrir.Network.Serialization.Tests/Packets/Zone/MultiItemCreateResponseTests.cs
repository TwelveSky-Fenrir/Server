using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcMultiItemCreateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(72, MultiItemCreateResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MultiItemCreate, MultiItemCreateResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[MultiItemCreateResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[72];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static void AssertFieldsEqual(MultiItemCreateResponse expected, MultiItemCreateResponse actual)
    {
        Assert.Equal(expected.Num, actual.Num);
        Assert.Equal(expected.Page, actual.Page);
        Assert.Equal(expected.Index1, actual.Index1);
        Assert.Equal(expected.Index2, actual.Index2);
        Assert.Equal(expected.Xy1, actual.Xy1);
        Assert.Equal(expected.Xy2, actual.Xy2);
        Assert.True(expected.ItemIndex.AsSpan().SequenceEqual(actual.ItemIndex));
        Assert.True(expected.Value.AsSpan().SequenceEqual(actual.Value));
    }

    private static MultiItemCreateResponse CreatePopulated()
    {
        return new MultiItemCreateResponse
        {
            Num = 11,
            Page = 22,
            Index1 = 33,
            Index2 = 44,
            Xy1 = 55,
            Xy2 = 66,
            ItemIndex = [100, 101, 102, 103, 104, 105, 106, 107],
            Value = [200, 201, 202, 203]
        };
    }

    private static void EncodeGolden(Span<byte> destination, MultiItemCreateResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Num);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Page);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Index1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.Index2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Xy1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], value.Xy2);
        for (var i = 0; i < 8; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(24 + i * 4)..], value.ItemIndex[i]);
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(56 + i * 4)..], value.Value[i]);
    }
}
