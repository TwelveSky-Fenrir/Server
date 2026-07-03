using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcProcessQuestRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, QuestProgressResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.QuestProgress, QuestProgressResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[20];
        value.Write(actual);

        var expected = new byte[20];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static QuestProgressResponse CreatePopulated()
    {
        return new QuestProgressResponse
        {
            Sort = 1,
            Page = 2,
            Index = 3,
            XPost = 4,
            YPost = 5
        };
    }

    private static void EncodeGolden(Span<byte> destination, QuestProgressResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Sort);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Page);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Index);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.XPost);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.YPost);
    }
}
