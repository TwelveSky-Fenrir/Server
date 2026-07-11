using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGuildWorkRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(1396, GuildActionResponse.PayloadSize);
        Assert.Equal(4 + 4 + GuildInfo.WireSize, GuildActionResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildAction, GuildActionResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var guildInfo = CreatePopulatedGuildInfo();
        var packet = new GuildActionResponse { Result = 0, Sort = 1, GuildInfo = guildInfo };

        Span<byte> buffer = new byte[GuildActionResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GuildActionResponse.PayloadSize, written);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer[4..]));

        var ok = GuildInfo.TryRead(buffer.Slice(8, GuildInfo.WireSize), out var decodedGuildInfo);
        Assert.True(ok);
        Assert.Equal(guildInfo.Name, decodedGuildInfo.Name);
        Assert.Equal(guildInfo.Grade, decodedGuildInfo.Grade);
        Assert.Equal(guildInfo.ChangeLeader, decodedGuildInfo.ChangeLeader);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var guildInfo = CreatePopulatedGuildInfo();
        var packet = new GuildActionResponse { Result = 2, Sort = 17, GuildInfo = guildInfo };

        Span<byte> buffer = new byte[GuildActionResponse.PayloadSize];
        packet.Write(buffer);

        var golden = new byte[1396];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 2);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 17);
        EncodeGuildInfoGolden(golden.AsSpan(8, GuildInfo.WireSize), guildInfo);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }

    [Fact]
    public void Write_ZeroFillsGuildInfo_OnFailureResponse()
    {
        var zeroGuildInfo = new GuildInfo
        {
            Name = string.Empty,
            Grade = 0,
            Master = string.Empty,
            SubMaster1 = string.Empty,
            SubMaster2 = string.Empty,
            MemberNames = new string[50],
            MemberRoles = new int[50],
            MemberCallNames = new string[50],
            Notices = new string[4],
            Point = 0,
            BuffType = 0,
            BuffState = 0,
            BuffTime = 0,
            ChangeLeader = 0
        };
        for (var i = 0; i < 50; i++)
        {
            zeroGuildInfo.MemberNames[i] = string.Empty;
            zeroGuildInfo.MemberCallNames[i] = string.Empty;
        }

        for (var i = 0; i < 4; i++)
            zeroGuildInfo.Notices[i] = string.Empty;

        var packet = new GuildActionResponse { Result = 1, Sort = 5, GuildInfo = zeroGuildInfo };

        Span<byte> buffer = new byte[GuildActionResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(buffer[8..].ToArray().All(b => b == 0));
    }

    private static GuildInfo CreatePopulatedGuildInfo()
    {
        var memberNames = new string[50];
        var memberRoles = new int[50];
        var memberCallNames = new string[50];
        for (var i = 0; i < 50; i++)
        {
            memberNames[i] = "M" + i;
            memberRoles[i] = i % 3;
            memberCallNames[i] = "C" + i;
        }

        var notices = new string[4];
        for (var i = 0; i < 4; i++)
            notices[i] = "Notice" + i;

        return new GuildInfo
        {
            Name = "Aesir",
            Grade = 3,
            Master = "Odin",
            SubMaster1 = "Thor",
            SubMaster2 = "Loki",
            MemberNames = memberNames,
            MemberRoles = memberRoles,
            MemberCallNames = memberCallNames,
            Notices = notices,
            Point = 4200,
            BuffType = 2,
            BuffState = 1,
            BuffTime = 30,
            ChangeLeader = 0
        };
    }

    private static void EncodeGuildInfoGolden(Span<byte> destination, GuildInfo value)
    {
        Encoding.Latin1.GetBytes(value.Name, destination[..13]);
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
