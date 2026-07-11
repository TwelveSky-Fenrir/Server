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
}
