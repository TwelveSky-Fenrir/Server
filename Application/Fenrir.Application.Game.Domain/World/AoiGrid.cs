namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Interest-management grid; cells partition X/Z since the wire's <c>ACTION_INFO.aLocation</c> uses Y as height.
///     Not thread-safe -- touched only from <see cref="Zone.RunAsync" />.
/// </summary>
public sealed class AoiGrid(float cellSize)
{
    private readonly Dictionary<(int X, int Z), HashSet<int>> _cells = new();

    public (int X, int Z) CellOf(float posX, float posZ)
    {
        return ((int)MathF.Floor(posX / cellSize), (int)MathF.Floor(posZ / cellSize));
    }

    public void Add(int characterId, (int X, int Z) cell)
    {
        if (!_cells.TryGetValue(cell, out var members))
            _cells[cell] = members = [];

        members.Add(characterId);
    }

    public void Remove(int characterId, (int X, int Z) cell)
    {
        if (!_cells.TryGetValue(cell, out var members))
            return;

        members.Remove(characterId);
        if (members.Count == 0)
            _cells.Remove(cell);
    }

    public void Move(int characterId, (int X, int Z) from, (int X, int Z) to)
    {
        if (from == to)
            return;

        Remove(characterId, from);
        Add(characterId, to);
    }

    /// <summary>Includes <paramref name="cell" /> itself; callers filter out self if needed.</summary>
    public IEnumerable<int> Neighbors((int X, int Z) cell)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    yield return id;
    }
}
