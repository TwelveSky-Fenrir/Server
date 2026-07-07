namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Interest-management grid; cells partition X/Z since the wire's <c>ACTION_INFO.aLocation</c> uses Y as height.
///     Not thread-safe -- touched only from <see cref="Zone.RunAsync" />.
/// </summary>
/// <remarks>
///     <see cref="Neighbors" />'s fixed 3x3 scan is the coarse-cell-index half of legacy's own two-stage
///     visibility check (<c>Broadcast11</c>, Server/ts25zone/S07_MyGame03.cpp:796-856): legacy computes a per-axis
///     cell index as position divided by a base unit radius and rejects a candidate whose index differs from the
///     broadcaster's by more than a scale factor (1, 2, or 3 depending on object type) on any axis, then applies
///     an exact 3D Euclidean distance check against that same scaled radius to the survivors. This grid's own cell
///     size (<see cref="GameServerOptions.AoiCellSize" />) is intentionally set to legacy's base unit radius (see
///     that property's own remarks) so that this fixed +/-1-cell scan reproduces legacy's "scale 1" coarse filter
///     exactly -- but it stops there: there is no second, exact-distance pass (a survivor of the coarse cell test
///     is unconditionally "visible" here, however far from the broadcaster within those 9 cells), no per-object-
///     type scale widening to +/-2 or +/-3 cells for legacy's special monster categories, and no height (Y) axis
///     in the cell partition itself. All three gaps are open questions the translating behavior contract flagged
///     as unresolved rather than something to guess at -- see <see cref="GameServerOptions.AoiCellSize" />'s own
///     remarks for the citations.
/// </remarks>
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

    /// <summary>
    ///     Non-allocating counterpart to <see cref="Neighbors" /> for hot per-packet call sites: appends every
    ///     character id in the 3x3 neighborhood of <paramref name="cell" /> to <paramref name="buffer" />,
    ///     excluding <paramref name="excludeCharacterId" />. <see cref="Neighbors" /> is a <c>yield return</c>
    ///     iterator, so every call allocates a compiler-generated enumerator; chaining a LINQ
    ///     <c>Where(id => id != x)</c> onto it additionally allocates a closure (the excluded id varies per
    ///     call, so the delegate can't be cached) plus the `Where` wrapper, and a trailing <c>ToArray()</c>
    ///     buffers through a growable structure before producing the final array. This method does the same
    ///     3x3 scan with a plain nested loop and an inline filter -- no iterator, no delegate, no LINQ.
    ///     Callers should reuse a single scratch <see cref="List{T}" /> across calls (cleared immediately
    ///     before each call) rather than allocating a fresh one every time.
    /// </summary>
    public void NeighborsExcludingSelf(List<int> buffer, (int X, int Z) cell, int excludeCharacterId)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    if (id != excludeCharacterId)
                        buffer.Add(id);
    }
}
