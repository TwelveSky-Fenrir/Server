using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcAvatarChangeInfo1Tests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(24, AvatarStateFlagResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AvatarStateFlag, AvatarStateFlagResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new AvatarStateFlagResponse
        {
            ServerIndex = 5,
            UniqueNumber = 123_456_789u,
            Sort = 7,
            Value01 = 1,
            Value02 = 2,
            Value03 = 3
        };

        Span<byte> buffer = new byte[AvatarStateFlagResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(AvatarStateFlagResponse.PayloadSize, written);
        Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(123_456_789u, BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]));
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(buffer[8..]));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer[12..]));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(buffer[16..]));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(buffer[20..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new AvatarStateFlagResponse
        {
            ServerIndex = 5,
            UniqueNumber = 123_456_789u,
            Sort = 7,
            Value01 = 1,
            Value02 = 2,
            Value03 = 3
        };

        var golden = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 5);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), 123_456_789u);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 7);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 2);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(20), 3);

        Span<byte> buffer = new byte[AvatarStateFlagResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
