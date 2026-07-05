using Fenrir.Application.Game.Progression;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class HeroRankBuilderTests
{
    private static HeroRankingRowDto Row(int characterId, string name, byte? tribe, int points)
    {
        return new HeroRankingRowDto(characterId, name, tribe, points, null, null, null, DateTime.UnixEpoch);
    }

    [Fact]
    public void MixedTribes_AreSplitIntoPerTribeTop10Slots()
    {
        var rows = new[]
        {
            Row(1, "Alice", 0, 500),
            Row(2, "Bob", 1, 400),
            Row(3, "Carl", 0, 300),
            Row(4, "Dana", 1, 200)
        };

        var grid = HeroRankBuilder.Build(rows);

        Assert.Equal("Alice", grid.Name[0 * 10 + 0]);
        Assert.Equal(500, grid.Point[0 * 10 + 0]);
        Assert.Equal("Carl", grid.Name[0 * 10 + 1]);
        Assert.Equal(300, grid.Point[0 * 10 + 1]);

        Assert.Equal("Bob", grid.Name[1 * 10 + 0]);
        Assert.Equal(400, grid.Point[1 * 10 + 0]);
        Assert.Equal("Dana", grid.Name[1 * 10 + 1]);
        Assert.Equal(200, grid.Point[1 * 10 + 1]);
    }

    [Fact]
    public void EmptySlots_DefaultToBlankNameAndZeroPoints()
    {
        var rows = new[] { Row(1, "Alice", 0, 500) };

        var grid = HeroRankBuilder.Build(rows);

        Assert.Equal(40, grid.Name.Length);
        Assert.Equal(40, grid.Point.Length);
        Assert.Equal(string.Empty, grid.Name[1]);
        Assert.Equal(0, grid.Point[1]);
        Assert.Equal(string.Empty, grid.Name[39]);
    }

    [Fact]
    public void MoreThanTenPerTribe_DropsOverflowBeyondRankTen()
    {
        var rows = Enumerable.Range(0, 12)
            .Select(i => Row(100 + i, $"P{i}", 2, 1000 - i))
            .ToArray();

        var grid = HeroRankBuilder.Build(rows);

        for (var rank = 0; rank < 10; rank++)
            Assert.Equal($"P{rank}", grid.Name[2 * 10 + rank]);

        Assert.DoesNotContain("P10", grid.Name);
        Assert.DoesNotContain("P11", grid.Name);
    }

    [Fact]
    public void NullOrOutOfRangeTribeId_IsIgnored()
    {
        var rows = new[]
        {
            Row(1, "NoTribe", null, 999),
            Row(2, "BadTribe", 4, 900),
            Row(3, "Valid", 3, 800)
        };

        var grid = HeroRankBuilder.Build(rows);

        Assert.DoesNotContain("NoTribe", grid.Name);
        Assert.DoesNotContain("BadTribe", grid.Name);
        Assert.Equal("Valid", grid.Name[3 * 10 + 0]);
    }
}
