using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

// Golden encoder is hand-built from the C++ GUILD_INFO layout (3 natural-alignment padding zones), independent of the generated Write.
public class GuildInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(1388, GuildInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildInfo.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildInfo.WireSize, written);

        Assert.True(GuildInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[1388];
        value.Write(actual);

        var expected = new byte[1388];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Write_ZeroesAllThreePaddingZones()
    {
        var value = CreatePopulated();

        var actual = new byte[1388];
        value.Write(actual);

        Assert.Equal(0, actual[13]);
        Assert.Equal(0, actual[14]);
        Assert.Equal(0, actual[15]);

        Assert.Equal(0, actual[709]);
        Assert.Equal(0, actual[710]);
        Assert.Equal(0, actual[711]);

        Assert.Equal(0, actual[1366]);
        Assert.Equal(0, actual[1367]);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[1388];
        EncodeGolden(golden, value);

        Assert.True(GuildInfo.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildInfo.TryRead(new byte[1387], out _));
    }

    private static GuildInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildInfo
        {
            Name = v.NextString(13),
            Grade = v.NextInt(),
            Master = v.NextString(13),
            SubMaster1 = v.NextString(13),
            SubMaster2 = v.NextString(13),
            MemberNames = v.NextStringArray(50, 13),
            MemberRoles = v.NextIntArray(50),
            MemberCallNames = v.NextStringArray(50, 5),
            Notices = v.NextStringArray(4, 51),
            Point = v.NextInt(),
            BuffType = v.NextInt(),
            BuffState = v.NextInt(),
            BuffTime = v.NextInt(),
            ChangeLeader = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, GuildInfo value)
    {
        Encoding.Latin1.GetBytes(value.Name, destination.Slice(0, 13));
        destination.Slice(13, 3).Clear();
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Grade);
        Encoding.Latin1.GetBytes(value.Master, destination.Slice(20, 13));
        Encoding.Latin1.GetBytes(value.SubMaster1, destination.Slice(33, 13));
        Encoding.Latin1.GetBytes(value.SubMaster2, destination.Slice(46, 13));

        for (var i = 0; i < 50; i++)
            Encoding.Latin1.GetBytes(value.MemberNames[i], destination.Slice(59 + i * 13, 13));

        destination.Slice(709, 3).Clear();

        for (var i = 0; i < 50; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(712 + i * 4)..], value.MemberRoles[i]);

        for (var i = 0; i < 50; i++)
            Encoding.Latin1.GetBytes(value.MemberCallNames[i], destination.Slice(912 + i * 5, 5));

        for (var i = 0; i < 4; i++)
            Encoding.Latin1.GetBytes(value.Notices[i], destination.Slice(1162 + i * 51, 51));

        destination.Slice(1366, 2).Clear();

        BinaryPrimitives.WriteInt32LittleEndian(destination[1368..], value.Point);
        BinaryPrimitives.WriteInt32LittleEndian(destination[1372..], value.BuffType);
        BinaryPrimitives.WriteInt32LittleEndian(destination[1376..], value.BuffState);
        BinaryPrimitives.WriteInt32LittleEndian(destination[1380..], value.BuffTime);
        BinaryPrimitives.WriteInt32LittleEndian(destination[1384..], value.ChangeLeader);
    }
}
