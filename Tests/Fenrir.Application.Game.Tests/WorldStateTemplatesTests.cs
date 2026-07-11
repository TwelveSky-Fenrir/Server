using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests;

public class WorldStateTemplatesTests
{
    [Fact]
    public void ZeroedWorldInfo_RoundTripsThroughWireFormat()
    {
        var worldInfo = WorldStateTemplates.ZeroedWorldInfo;

        Assert.Equal(4, worldInfo.TribeSymbol.Length);
        Assert.Equal(4, worldInfo.TribePoint.Length);
        Assert.Equal(2, worldInfo.TribeCloseInfo.Length);
        Assert.Equal(8, worldInfo.PossibleAllianceInfo.Length);
        Assert.Equal(4, worldInfo.AllianceState.Length);
        Assert.Equal(4, worldInfo.TribeVoteState.Length);
        Assert.Equal(4, worldInfo.CloseVoteState.Length);
        Assert.Equal(13, worldInfo.Zone049TypeState.Length);
        Assert.Equal(13, worldInfo.Zone049TypeStateTime.Length);
        Assert.Equal(6, worldInfo.Zone051TypeState.Length);
        Assert.Equal(6, worldInfo.Zone051TypeStateTime.Length);
        Assert.Equal(10, worldInfo.Zone053TypeState.Length);
        Assert.Equal(10, worldInfo.Zone053TypeStateTime.Length);
        Assert.Equal(32, worldInfo.Zone175TypeState.Length);
        Assert.Equal(16, worldInfo.TribeGuardState.Length);
        Assert.Equal(3, worldInfo.Zone297TypeState.Length);
        Assert.Equal(3, worldInfo.Zone297TypeBegin.Length);
        Assert.Equal(4, worldInfo.Zone038DTMValue.Length);
        Assert.Equal(4, worldInfo.TribeGeneralExperienceUpRatioInfo.Length);
        Assert.Equal(4, worldInfo.TribeItemDropUpRatioInfo.Length);
        Assert.Equal(4, worldInfo.TribeItemDropUpRatioForMyoungInfo.Length);
        Assert.Equal(4, worldInfo.TribeKillOtherTribeAddValueInfo.Length);
        Assert.Equal(4, worldInfo.TribeMasterCallAbility.Length);
        Assert.Equal(4, worldInfo.Zone267TypeState.Length);
        Assert.Equal(20, worldInfo.Zone241TypeState.Length);
        Assert.Equal(5, worldInfo.Zone270TypeState.Length);
        Assert.Equal(3, worldInfo.GuildName3.Length);
        Assert.Equal(3, worldInfo.GuildScore.Length);
        Assert.Equal(4, worldInfo.FourGuildState.Length);
        Assert.Equal(16, worldInfo.FourGuildName.Length);
        Assert.Equal(4, worldInfo.DecideChallengeFourGuildName.Length);
        Assert.Equal(4, worldInfo.Zone5TypeState.Length);
        Assert.Equal(4, worldInfo.TribeNokSanStone.Length);
        Assert.Equal(9, worldInfo.NokSanStoneState.Length);
        Assert.Equal(5, worldInfo.Zone319TypeState.Length);
        Assert.Equal(5, worldInfo.Unk6.Length);
        Assert.Equal(5, worldInfo.PopUpTypeState.Length);
        Assert.Equal(5, worldInfo.PopUpKillAvt.Length);
        Assert.Equal(5, worldInfo.PopUpKillMonster.Length);

        Assert.All(worldInfo.GuildName3, Assert.NotNull);
        Assert.All(worldInfo.FourGuildName, Assert.NotNull);
        Assert.All(worldInfo.DecideChallengeFourGuildName, Assert.NotNull);

        var buffer = new byte[WorldInfo.WireSize];
        var written = worldInfo.Write(buffer);

        Assert.Equal(WorldInfo.WireSize, written);
        Assert.True(WorldInfo.TryRead(buffer, out var read));
        Assert.Equal(WorldInfo.WireSize, read.Write(new byte[WorldInfo.WireSize]));
    }

    [Fact]
    public void ZeroedTribeInfo_RoundTripsThroughWireFormat()
    {
        var tribeInfo = WorldStateTemplates.ZeroedTribeInfo;

        Assert.Equal(40, tribeInfo.TribeVoteName.Length);
        Assert.Equal(40, tribeInfo.TribeVoteLevel.Length);
        Assert.Equal(40, tribeInfo.TribeVoteKillOtherTribe.Length);
        Assert.Equal(40, tribeInfo.TribeVotePoint.Length);
        Assert.Equal(4, tribeInfo.TribeMaster.Length);
        Assert.Equal(48, tribeInfo.TribeSubMaster.Length);
        Assert.Equal(20, tribeInfo.HoisundoName1.Length);
        Assert.Equal(20, tribeInfo.HoisundoName2.Length);
        Assert.Equal(20, tribeInfo.HoisundoName3.Length);

        Assert.All(tribeInfo.TribeVoteName, Assert.NotNull);
        Assert.All(tribeInfo.TribeMaster, Assert.NotNull);
        Assert.All(tribeInfo.TribeSubMaster, Assert.NotNull);
        Assert.All(tribeInfo.HoisundoName1, Assert.NotNull);
        Assert.All(tribeInfo.HoisundoName2, Assert.NotNull);
        Assert.All(tribeInfo.HoisundoName3, Assert.NotNull);

        var buffer = new byte[TribeInfo.WireSize];
        var written = tribeInfo.Write(buffer);

        Assert.Equal(TribeInfo.WireSize, written);
        Assert.True(TribeInfo.TryRead(buffer, out var read));
        Assert.Equal(TribeInfo.WireSize, read.Write(new byte[TribeInfo.WireSize]));
    }

    [Fact]
    public void ZeroedBuffInfo_RoundTripsThroughWireFormat()
    {
        var buffInfo = WorldStateTemplates.ZeroedBuffInfo;

        Assert.Equal(70, buffInfo.Buff.Length);

        var buffer = new byte[BuffInfo.WireSize];
        var written = buffInfo.Write(buffer);

        Assert.Equal(BuffInfo.WireSize, written);
        Assert.True(BuffInfo.TryRead(buffer, out var read));
        Assert.Equal(BuffInfo.WireSize, read.Write(new byte[BuffInfo.WireSize]));
    }
}
