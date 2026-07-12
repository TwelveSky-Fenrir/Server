using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

public class AoiGridTests
{
    private static readonly float CellSize = 100f;

    [Fact]
    public void HasAnyNeighbor_EmptyGrid_ReturnsFalse()
    {
        var grid = new AoiGrid(CellSize);

        Assert.False(grid.HasAnyNeighbor((0, 0)));
    }

    [Fact]
    public void HasAnyNeighbor_TrueForTheOccupiedCellItself()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (5, 5));

        Assert.True(grid.HasAnyNeighbor((5, 5)));
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, -1)]
    [InlineData(1, -1)]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void HasAnyNeighbor_TrueFromEachOfTheEightAdjacentCells(int dx, int dz)
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (5, 5));

        Assert.True(grid.HasAnyNeighbor((5 + dx, 5 + dz)));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(-2, 0)]
    [InlineData(0, 2)]
    [InlineData(0, -2)]
    [InlineData(2, 2)]
    public void HasAnyNeighbor_FalseOutsideThe3x3Neighborhood(int dx, int dz)
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (5, 5));

        Assert.False(grid.HasAnyNeighbor((5 + dx, 5 + dz)));
    }

    [Fact]
    public void HasAnyNeighbor_FalseAgain_AfterTheLastOccupantOfTheCellIsRemoved()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (5, 5));
        grid.Remove(1, (5, 5));

        Assert.False(grid.HasAnyNeighbor((5, 5)));
    }

    [Fact]
    public void HasAnyNeighbor_StaysTrue_WhileAtLeastOneOccupantRemainsInTheCell()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (5, 5));
        grid.Add(2, (5, 5));
        grid.Remove(1, (5, 5));

        Assert.True(grid.HasAnyNeighbor((5, 5)));
    }

    [Fact]
    public void HasAnyNeighbor_ReflectsAMoveOutOfAndIntoRange()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (5, 5));

        grid.Move(1, (5, 5), (50, 50));

        Assert.False(grid.HasAnyNeighbor((5, 5)));
        Assert.True(grid.HasAnyNeighbor((50, 50)));
    }

    [Fact]
    public void HasAnyNeighbor_AgreesWithNeighbors_AcrossARandomlyPopulatedGrid()
    {
        var grid = new AoiGrid(CellSize);
        var random = new Random(20260707);
        for (var id = 0; id < 200; id++)
            grid.Add(id, (random.Next(-10, 11), random.Next(-10, 11)));

        for (var x = -12; x <= 12; x++)
        for (var z = -12; z <= 12; z++)
        {
            var cell = (x, z);
            Assert.Equal(grid.Neighbors(cell).Any(), grid.HasAnyNeighbor(cell));
        }
    }

    [Fact]
    public void Neighbors_WithBuffer_ReturnsEveryCoOccupantOfTheCell_NoMatterHowManyThereAre()
    {
        var grid = new AoiGrid(CellSize);
        const int occupantCount = 5000;
        for (var id = 0; id < occupantCount; id++)
            grid.Add(id, (5, 5), 500f, 0f, 500f);

        var buffer = new List<int>();
        grid.Neighbors(buffer, (5, 5), 500f, 0f, 500f);

        Assert.Equal(occupantCount, buffer.Count);
    }

    [Fact]
    public void NeighborsExcludingSelf_WithBuffer_ReturnsEveryOtherCoOccupant_NoMatterHowManyThereAre()
    {
        var grid = new AoiGrid(CellSize);
        const int occupantCount = 5000;
        for (var id = 0; id < occupantCount; id++)
            grid.Add(id, (5, 5), 500f, 0f, 500f);

        var buffer = new List<int>();
        grid.NeighborsExcludingSelf(buffer, (5, 5), 0, 500f, 0f, 500f);

        Assert.Equal(occupantCount - 1, buffer.Count);
        Assert.DoesNotContain(0, buffer);
    }

    [Fact]
    public void Neighbors_WithOrigin_AcceptsExactlyAtTheScaledRadiusBoundary()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (1, 0), CellSize, 0f, 0f);

        var buffer = new List<int>();
        grid.Neighbors(buffer, (0, 0), 0f, 0f, 0f);

        Assert.Contains(1, buffer);
    }

    [Fact]
    public void Neighbors_WithOrigin_RejectsJustBeyondTheScaledRadiusBoundary()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (1, 0), CellSize + 1f, 0f, 0f);

        var buffer = new List<int>();
        grid.Neighbors(buffer, (0, 0), 0f, 0f, 0f);

        Assert.DoesNotContain(1, buffer);
    }

    [Fact]
    public void Neighbors_WithOrigin_HonorsTheYAxisInTheExactDistanceStage()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (0, 0), 0f, CellSize + 1f, 0f);

        var buffer = new List<int>();
        grid.Neighbors(buffer, (0, 0), 0f, 0f, 0f);

        Assert.DoesNotContain(1, buffer);
    }

    [Fact]
    public void Neighbors_WithOrigin_ScaleWidensBothTheCoarseBoxAndTheExactRadius()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (2, 0), CellSize * 2, 0f, 0f);

        var unscaled = new List<int>();
        grid.Neighbors(unscaled, (0, 0), 0f, 0f, 0f);
        Assert.DoesNotContain(1, unscaled);

        var scaled = new List<int>();
        grid.Neighbors(scaled, (0, 0), 0f, 0f, 0f, scale: 2);
        Assert.Contains(1, scaled);
    }

    [Fact]
    public void Neighbors_WithOrigin_ExcludesACandidateWithNoTrackedPosition()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (0, 0));

        var buffer = new List<int>();
        grid.Neighbors(buffer, (0, 0), 0f, 0f, 0f);

        Assert.DoesNotContain(1, buffer);
    }

    [Fact]
    public void IsWithinRadius_AcceptsExactlyAtTheScaledRadiusBoundary()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (1, 0), CellSize * 3, 0f, 0f);

        Assert.True(grid.IsWithinRadius(1, 0f, 0f, 0f, 3));
    }

    [Fact]
    public void IsWithinRadius_RejectsJustBeyondTheScaledRadiusBoundary()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (1, 0), CellSize * 3 + 1f, 0f, 0f);

        Assert.False(grid.IsWithinRadius(1, 0f, 0f, 0f, 3));
    }

    [Fact]
    public void IsWithinRadius_RejectsAnOtherwiseInRangeCandidate_WhenTheSmallerScaleNoLongerCoversIt()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (1, 0), CellSize * 2, 0f, 0f);

        Assert.True(grid.IsWithinRadius(1, 0f, 0f, 0f, 3));
        Assert.False(grid.IsWithinRadius(1, 0f, 0f, 0f, 1));
    }

    [Fact]
    public void IsWithinRadius_FailsClosed_ForACandidateWithNoTrackedPosition()
    {
        var grid = new AoiGrid(CellSize);
        grid.Add(1, (0, 0));

        Assert.False(grid.IsWithinRadius(1, 0f, 0f, 0f, 3));
    }
}
