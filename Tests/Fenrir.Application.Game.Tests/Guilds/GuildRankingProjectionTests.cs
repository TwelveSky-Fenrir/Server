using System.Collections.Immutable;
using Fenrir.Application.Game.Guilds;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildRankingProjectionTests
{
    [Fact]
    public void Apply_FewerThanThreeGuilds_FillsRemainingSlotsBlank()
    {
        var template = EmptyTemplate();
        var top = ImmutableArray.Create(new GuildRankingRowDto(1, "Aesir", 500));

        var result = GuildRankingProjection.Apply(template, top);

        Assert.Equal(["Aesir", "", ""], result.GuildName3);
        Assert.Equal([500, 0, 0], result.GuildScore);
    }

    [Fact]
    public void Apply_ThreeGuilds_FillsAllSlotsInOrder()
    {
        var template = EmptyTemplate();
        var top = ImmutableArray.Create(
            new GuildRankingRowDto(1, "Aesir", 900),
            new GuildRankingRowDto(2, "Vanir", 500),
            new GuildRankingRowDto(3, "Jotnar", 100));

        var result = GuildRankingProjection.Apply(template, top);

        Assert.Equal(["Aesir", "Vanir", "Jotnar"], result.GuildName3);
        Assert.Equal([900, 500, 100], result.GuildScore);
    }

    [Fact]
    public void Apply_MoreThanThreeGuilds_KeepsOnlyTheTopThree()
    {
        var template = EmptyTemplate();
        var top = ImmutableArray.Create(
            new GuildRankingRowDto(1, "Aesir", 900),
            new GuildRankingRowDto(2, "Vanir", 500),
            new GuildRankingRowDto(3, "Jotnar", 100),
            new GuildRankingRowDto(4, "Alfar", 1));

        var result = GuildRankingProjection.Apply(template, top);

        Assert.Equal(["Aesir", "Vanir", "Jotnar"], result.GuildName3);
        Assert.Equal([900, 500, 100], result.GuildScore);
    }

    [Fact]
    public void Apply_NoGuilds_LeavesEveryRankingSlotBlank()
    {
        var template = EmptyTemplate();

        var result = GuildRankingProjection.Apply(template, []);

        Assert.Equal(["", "", ""], result.GuildName3);
        Assert.Equal([0, 0, 0], result.GuildScore);
    }

    [Fact]
    public void Apply_LeavesEveryOtherWorldInfoFieldUntouched()
    {
        var template = EmptyTemplate() with { GuildBattle = 7, GuildName1 = "Untouched" };

        var result = GuildRankingProjection.Apply(template,
            ImmutableArray.Create(new GuildRankingRowDto(1, "Aesir", 900)));

        Assert.Equal(7, result.GuildBattle);
        Assert.Equal("Untouched", result.GuildName1);
    }

    private static WorldInfo EmptyTemplate()
    {
        return WorldStateTemplates.ZeroedWorldInfo;
    }
}
