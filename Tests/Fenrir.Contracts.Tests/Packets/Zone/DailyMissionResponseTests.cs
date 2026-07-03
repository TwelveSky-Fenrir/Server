using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcMissionCompleteRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(24, DailyMissionResponse.PayloadSize);
        Assert.Equal(4 + 4 + MissionDate.WireSize, DailyMissionResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DailyMission, DailyMissionResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[24];
        value.Write(actual);

        var expected = new byte[24];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static DailyMissionResponse CreatePopulated()
    {
        return new DailyMissionResponse
        {
            Sort = 2,
            Result = 0,
            Mission = new MissionDate
            {
                JoinWar = 0,
                KillOtherTribe = 5,
                KillMonster = 0,
                PlayTime = 120
            }
        };
    }

    private static void EncodeGolden(Span<byte> destination, DailyMissionResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Sort);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Mission.JoinWar);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.Mission.KillOtherTribe);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Mission.KillMonster);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], value.Mission.PlayTime);
    }
}
