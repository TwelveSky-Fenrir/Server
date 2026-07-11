using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class WorldInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(1384, WorldInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var worldInfo = new WorldInfo
        {
            Zone038WinTribe = v.NextInt(),
            Zone038WinTribeTime = v.NextInt(),
            TribeSymbolBattle = v.NextInt(),
            TribeSymbol = v.NextIntArray(4),
            MonsterSymbol = v.NextInt(),
            MonsterSymbolEndTime = v.NextInt(),
            TribePoint = v.NextIntArray(4),
            TribeCloseInfo = v.NextIntArray(2),
            PossibleAllianceInfo = v.NextIntArray(8),
            AllianceState = v.NextIntArray(4),
            TribeVoteState = v.NextIntArray(4),
            CloseVoteState = v.NextIntArray(4),
            Tribe4QuestDate = v.NextInt(),
            Tribe4QuestState = v.NextInt(),
            Tribe4QuestName = v.NextString(13),
            Zone049TypeState = v.NextIntArray(13),
            Zone049TypeStateTime = v.NextIntArray(13),
            Zone051TypeState = v.NextIntArray(6),
            Zone051TypeStateTime = v.NextIntArray(6),
            Zone053TypeState = v.NextIntArray(10),
            Zone053TypeStateTime = v.NextIntArray(10),
            Zone175TypeState = v.NextIntArray(32),
            TribeGuardState = v.NextIntArray(16),
            Zone194TypeState = v.NextInt(),
            Zone297TypeState = v.NextIntArray(3),
            Zone297TypeBegin = v.NextIntArray(3),
            Zone038DTMValue = v.NextIntArray(4),
            TribeGeneralExperienceUpRatioInfo = v.NextFloatArray(4),
            TribeItemDropUpRatioInfo = v.NextFloatArray(4),
            TribeItemDropUpRatioForMyoungInfo = v.NextFloatArray(4),
            TribeKillOtherTribeAddValueInfo = v.NextIntArray(4),
            TribeMasterCallAbility = v.NextIntArray(4),
            Zone267TypeState = v.NextIntArray(4),
            Zone241TypeState = v.NextIntArray(20),
            Zone270TypeState = v.NextIntArray(5),
            GuildBattle = v.NextInt(),
            GuildName1 = v.NextString(13),
            GuildName2 = v.NextString(13),
            GuildName3 = v.NextStringArray(3, 13),
            GuildScore = v.NextIntArray(3),
            Zone088WinTribe = v.NextInt(),
            FourGuildState = v.NextIntArray(4),
            FourGuildName = v.NextStringArray(16, 13),
            DecideChallengeFourGuildName = v.NextStringArray(4, 13),
            Zone200TypeState = v.NextInt(),
            Zone5TypeState = v.NextIntArray(4),
            Zone54TypeState = v.NextInt(),
            CountDrop8Cho = v.NextInt(),
            TribeNokSanStone = v.NextIntArray(4),
            NokSanStoneState = v.NextIntArray(9),
            Zone319TypeState = v.NextIntArray(5),
            WaterRainHeavenState = v.NextInt(),
            WaterRainHeavenStart = v.NextInt(),
            ZoneFFATypeState = v.NextInt(),
            Unk6 = v.NextIntArray(5),
            PopUpTypeState = v.NextIntArray(5),
            PopUpKillAvt = v.NextIntArray(5),
            PopUpKillMonster = v.NextIntArray(5)
        };

        var buffer = new byte[WorldInfo.WireSize];
        var written = worldInfo.Write(buffer);
        Assert.Equal(WorldInfo.WireSize, written);

        Assert.True(WorldInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(worldInfo, roundTripped);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[WorldInfo.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 555);
        Encoding.Latin1.GetBytes("Quest1", buffer.AsSpan(148, 16));
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(900, 4), 777);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(1364 + 4 * 4, 4), 999);

        var ok = WorldInfo.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(555, packet.Zone038WinTribe);
        Assert.Equal("Quest1", packet.Tribe4QuestName);
        Assert.Equal(777, packet.GuildScore[0]);
        Assert.Equal(999, packet.PopUpKillMonster[4]);
    }
}
