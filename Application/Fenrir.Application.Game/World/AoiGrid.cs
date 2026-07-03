namespace Fenrir.Application.Game.World;

/// <summary>
///     Interest management (architecture reference §10.3): uniform square partition of the map, cell side =
///     view radius. A player is visible only from its own cell and the 8 neighbors — O(entities in a 3x3 block),
///     never O(n²) over the whole zone. Horizontal plane is (X, Z): the legacy wire's <c>ACTION_INFO.aLocation</c>
///     is <c>[x, y, z]</c> with Y as vertical height (wire contract §7.1 golden example), so cells partition X/Z,
///     not X/Y. Not thread-safe by design — touched only from <see cref="Zone.RunAsync" />, same as every other
///     piece of zone state (§10.1: "un seul écrivain").
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

    /// <summary>
    ///     No-ops when <paramref name="from" /> and <paramref name="to" /> are the same cell — the common case every tick
    ///     a player doesn't cross a boundary.
    /// </summary>
    public void Move(int characterId, (int X, int Z) from, (int X, int Z) to)
    {
        if (from == to)
            return;

        Remove(characterId, from);
        Add(characterId, to);
    }

    /// <summary>
    ///     Every character id in <paramref name="cell" /> and its 8 neighbors. Includes whichever id occupies
    ///     <paramref name="cell" /> itself — callers that need to exclude self (e.g. a broadcast audience) filter it out at
    ///     the call site.
    /// </summary>
    public IEnumerable<int> Neighbors((int X, int Z) cell)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    yield return id;
    }
}
