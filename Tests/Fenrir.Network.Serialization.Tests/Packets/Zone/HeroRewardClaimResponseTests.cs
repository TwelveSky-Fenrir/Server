using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcHeroRewardRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(56, HeroRewardClaimResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.HeroRewardClaim, HeroRewardClaimResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[56];
        value.Write(actual);

        var expected = new byte[56];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static HeroRewardClaimResponse CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new HeroRewardClaimResponse
        {
            Result = 1000,
            Page = v.NextInt(),
            Index1 = v.NextInt(),
            Index2 = v.NextInt(),
            Xy1 = v.NextInt(),
            Xy2 = v.NextInt(),
            ItemIndex = v.NextIntArray(8)
        };
    }

    private static void EncodeGolden(Span<byte> destination, HeroRewardClaimResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Page);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Index1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.Index2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Xy1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], value.Xy2);
        for (var i = 0; i < 8; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(24 + i * 4)..], value.ItemIndex[i]);
    }
}
