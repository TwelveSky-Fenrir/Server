namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Interest-management grid; cells partition X/Z since the wire's <c>ACTION_INFO.aLocation</c> uses Y as height.
///     Not thread-safe -- touched only from <see cref="Zone.RunAsync" />.
/// </summary>
/// <remarks>
///     Legacy's own visibility check is two-stage (<c>Broadcast11</c>/<c>Broadcast22</c>,
///     Server/ts25zone/S07_MyGame03.cpp:796-985): a coarse per-axis cell-index test (position divided by a base
///     unit radius, rejecting a candidate whose index differs from the broadcaster's by more than a scale factor
///     -- 1, 2, or 3 depending on object type -- on any of the X/Y/Z axes, :805-833) followed by an exact 3D
///     squared-Euclidean-distance check against that same scaled radius applied to the survivors (:837-844). This
///     class's own cell size (<see cref="GameServerOptions.AoiCellSize" />) is intentionally set to legacy's base
///     unit radius (see that property's own remarks) so the coarse box scan below reproduces legacy's coarse
///     stage exactly for a given scale.
///     <para>
///         Both stages are now implemented. <see cref="Neighbors(ValueTuple{int,int},float,float,float,int)" />,
///         <see cref="NeighborsExcludingSelf(List{int},ValueTuple{int,int},int,float,float,float,int)" />, and
///         <see cref="Neighbors(List{int},ValueTuple{int,int},float,float,float,int)" /> each widen the coarse
///         box to <c>+/-scale</c> cells (legacy's own per-axis scale widening,
///         Server/Header/Protocol/DEFINE.h:141-143 <c>UNIT_SCALE_RADIUS1/2/3</c>) and then reject any coarse
///         survivor whose true squared distance from the supplied exact origin exceeds
///         <c>(cellSize * scale)^2</c> -- a direct port of the <c>Broadcast11</c> exact-distance test at
///         S07_MyGame03.cpp:838-844, using <c>&lt;=</c> to admit (legacy rejects on strictly-greater, :841).
///         Positions are tracked per member (<see cref="Add(int,ValueTuple{int,int},float,float,float)" />/
///         <see cref="Move(int,ValueTuple{int,int},ValueTuple{int,int},float,float,float)" />) specifically so
///         this exact-distance pass never needs a second dictionary hop through the caller's own player/monster
///         map -- see <see cref="Zone" />'s partial-class call sites for how each broadcaster's own live position
///         feeds the <c>originX/Y/Z</c> parameters.
///     </para>
///     <para>
///         The coarse box itself still partitions X/Z only, never Y (legacy's own coarse test independently
///         rejects on X, Y, OR Z alone, :833) -- but this is no longer a visibility-correctness gap now that the
///         exact-distance pass exists: omitting Y from the coarse box can only ever make the box a superset of
///         legacy's own coarse candidate set (never reject someone legacy's 3-axis coarse test would have kept),
///         and every survivor of that superset is still filtered down to the true 3D radius by the exact pass
///         below -- so the final recipient set is identical to legacy's either way, at the cost of visiting a
///         handful of extra (Y-only-divergent) cells before the exact filter trims them. Retained as a
///         deliberate, now-harmless simplification rather than "unresolved."
///     </para>
///     <para>
///         Per-object-type scale widening (2x/3x the base radius for a subset of monster categories) is resolved
///         by <see cref="Monsters.MonsterBroadcastScale" /> for the one broadcast family that has an unambiguous
///         legacy dispatch -- the periodic monster catch-up tick, which always resolves scale via
///         <c>MONSTER_OBJECT::SendSpecialNumber</c> (Server/ts25zone/S07_MyGame01.cpp:2566,
///         S07_MyGame05.cpp:3982-4001) -- see that class's own remarks for the full citation chain and for why
///         the monster-SPAWN broadcast (which legacy lets vary per summon call site,
///         S10_MySummon.cpp:854-871) reuses the same mapping as a deliberate simplification rather than modeling
///         each call site's own dispatch choice individually.
///     </para>
///     <para>
///         Every method below also keeps its original, position-agnostic coarse-only overload (no
///         <c>originX/Y/Z</c> parameters, no exact-distance filtering) for callers that only need cell
///         membership -- <see cref="HasAnyNeighbor" />'s own cheap emptiness pre-check is deliberately still
///         coarse-only even after this change (see its own remarks for why a coarse-only "maybe" answer is
///         still correct here). <see cref="Add(int,ValueTuple{int,int})" />/
///         <see cref="Move(int,ValueTuple{int,int},ValueTuple{int,int})" /> similarly stay available for a
///         caller that never needs the exact-distance pass and so never registers a position -- mixing the
///         position-tracked and position-agnostic overloads for the SAME member id is unsupported (the exact
///         pass fails closed -- treats the member as out of range -- for any id it has no tracked position for,
///         see <see cref="WithinExactRadius" />).
///     </para>
/// </remarks>
public sealed class AoiGrid(float cellSize)
{
    private readonly Dictionary<(int X, int Z), HashSet<int>> _cells = new();

    /// <summary>
    ///     Per-member exact position, populated only by the position-tracked <see cref="Add" />/
    ///     <see cref="Move" /> overloads -- see class remarks for why the coarse-only overloads deliberately
    ///     leave a member's entry here absent.
    /// </summary>
    private readonly Dictionary<int, (float X, float Y, float Z)> _positions = new();

    public (int X, int Z) CellOf(float posX, float posZ)
    {
        return ((int)MathF.Floor(posX / cellSize), (int)MathF.Floor(posZ / cellSize));
    }

    /// <summary>Coarse-only registration -- see class remarks. Prefer the position-tracked overload below.</summary>
    public void Add(int characterId, (int X, int Z) cell)
    {
        AddMembership(characterId, cell);
    }

    /// <summary>Registers <paramref name="characterId" /> for both the coarse box scan and the exact-distance pass.</summary>
    public void Add(int characterId, (int X, int Z) cell, float posX, float posY, float posZ)
    {
        AddMembership(characterId, cell);
        _positions[characterId] = (posX, posY, posZ);
    }

    public void Remove(int characterId, (int X, int Z) cell)
    {
        RemoveMembership(characterId, cell);
        _positions.Remove(characterId);
    }

    /// <summary>Coarse-only relocation -- see class remarks. Prefer the position-tracked overload below.</summary>
    public void Move(int characterId, (int X, int Z) from, (int X, int Z) to)
    {
        if (from == to)
            return;

        RemoveMembership(characterId, from);
        AddMembership(characterId, to);
    }

    /// <summary>
    ///     Relocates <paramref name="characterId" /> and refreshes its tracked exact position -- unlike the
    ///     coarse-only overload, this must still run even when <paramref name="from" /> equals
    ///     <paramref name="to" />, since the member can have moved within the same cell (the exact-distance pass
    ///     would otherwise keep comparing against a stale position).
    /// </summary>
    public void Move(int characterId, (int X, int Z) from, (int X, int Z) to, float posX, float posY, float posZ)
    {
        if (from != to)
        {
            RemoveMembership(characterId, from);
            AddMembership(characterId, to);
        }

        _positions[characterId] = (posX, posY, posZ);
    }

    private void AddMembership(int characterId, (int X, int Z) cell)
    {
        if (!_cells.TryGetValue(cell, out var members))
            _cells[cell] = members = [];

        members.Add(characterId);
    }

    private void RemoveMembership(int characterId, (int X, int Z) cell)
    {
        if (!_cells.TryGetValue(cell, out var members))
            return;

        members.Remove(characterId);
        if (members.Count == 0)
            _cells.Remove(cell);
    }

    /// <summary>Coarse-only, no exact-distance filtering. Includes <paramref name="cell" /> itself.</summary>
    public IEnumerable<int> Neighbors((int X, int Z) cell)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    yield return id;
    }

    /// <summary>
    ///     Legacy-parity two-stage query: widens the coarse box to <c>+/-scale</c> cells around
    ///     <paramref name="cell" />, then keeps only members whose tracked exact position is within
    ///     <c>cellSize * scale</c> units (3D) of <paramref name="originX" />/<paramref name="originY" />/
    ///     <paramref name="originZ" /> -- see class remarks for the legacy citation. Includes <paramref name="cell" />
    ///     itself.
    /// </summary>
    public IEnumerable<int> Neighbors((int X, int Z) cell, float originX, float originY, float originZ, int scale = 1)
    {
        var radiusSquared = ExactRadiusSquared(scale);
        for (var dx = -scale; dx <= scale; dx++)
        for (var dz = -scale; dz <= scale; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    if (WithinExactRadius(id, originX, originY, originZ, radiusSquared))
                        yield return id;
    }

    /// <summary>
    ///     Cheap emptiness pre-check for hot broadcast call sites (<see cref="Zone.BroadcastMonsterAction" />,
    ///     <see cref="Zone.BroadcastGroundItemAction" />, <see cref="Zone.BroadcastProxyShopState" />): true if
    ///     any of the <c>(2*scale+1)^2</c> candidate cells around <paramref name="cell" /> currently holds at
    ///     least one occupant. Those call sites used to compute <c>NeighborsOfPosition(...).ToArray()</c>
    ///     unconditionally just to test <c>Length == 0</c> before an early return -- allocating <see cref="Neighbors(ValueTuple{int,int})" />'s
    ///     own compiler-generated iterator plus a LINQ <see cref="Enumerable.ToArray{TSource}" /> buffer on every
    ///     single broadcast, even the overwhelmingly common case of a solitary monster/item/shop with nobody
    ///     nearby to tell. This does the same <c>+/-scale</c> box scan as <see cref="Neighbors(ValueTuple{int,int})" />
    ///     but only ever checks <see cref="Dictionary{TKey,TValue}.ContainsKey" /> -- it never walks a cell's
    ///     member set, and it returns as soon as the first occupied cell is found rather than always visiting
    ///     every candidate cell. Correct without inspecting membership because <see cref="Remove" /> already
    ///     deletes a cell's dictionary entry the moment its member set empties out (see that method), so "the
    ///     cell key is present" and "the cell has at least one occupant" are exactly the same condition here.
    ///     <para>
    ///         Deliberately stays coarse-only (no exact-distance check) even though every full query below now
    ///         has one: this method only ever answers "is a broadcast worth attempting at all," and a coarse
    ///         "maybe" is still a safe, cheap answer for that -- a coarse-empty box guarantees zero exact
    ///         survivors too (the exact pass can only ever shrink the coarse candidate set), while a coarse
    ///         non-empty box may still turn out to have zero exact survivors, which the caller's own subsequent
    ///         exact query already handles correctly. Adding position lookups here would only pay for a second
    ///         answer the real query is about to compute anyway.
    ///     </para>
    /// </summary>
    public bool HasAnyNeighbor((int X, int Z) cell, int scale = 1)
    {
        for (var dx = -scale; dx <= scale; dx++)
        for (var dz = -scale; dz <= scale; dz++)
            if (_cells.ContainsKey((cell.X + dx, cell.Z + dz)))
                return true;

        return false;
    }

    /// <summary>
    ///     Non-allocating, coarse-only counterpart to <see cref="Neighbors(ValueTuple{int,int})" /> for hot
    ///     per-packet call sites: appends every character id in the 3x3 neighborhood of <paramref name="cell" />
    ///     to <paramref name="buffer" />, excluding <paramref name="excludeCharacterId" />. <see cref="Neighbors(ValueTuple{int,int})" />
    ///     is a <c>yield return</c> iterator, so every call allocates a compiler-generated enumerator; chaining a
    ///     LINQ <c>Where(id => id != x)</c> onto it additionally allocates a closure (the excluded id varies per
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

    /// <summary>
    ///     Non-allocating, legacy-parity two-stage counterpart to <see cref="NeighborsExcludingSelf(List{int},ValueTuple{int,int},int)" />:
    ///     same <c>+/-scale</c> box scan and inline filter, plus the same exact-distance check documented on
    ///     <see cref="Neighbors(ValueTuple{int,int},float,float,float,int)" />. This is the overload every
    ///     production avatar-broadcast call site in <see cref="Zone" /> uses.
    /// </summary>
    public void NeighborsExcludingSelf(List<int> buffer, (int X, int Z) cell, int excludeCharacterId,
        float originX, float originY, float originZ, int scale = 1)
    {
        var radiusSquared = ExactRadiusSquared(scale);
        for (var dx = -scale; dx <= scale; dx++)
        for (var dz = -scale; dz <= scale; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    if (id != excludeCharacterId && WithinExactRadius(id, originX, originY, originZ, radiusSquared))
                        buffer.Add(id);
    }

    /// <summary>
    ///     Non-allocating, coarse-only counterpart to <see cref="Neighbors(ValueTuple{int,int})" /> for
    ///     non-player broadcasters (a monster or a ground item is never itself a member of this grid, so there
    ///     is no self id to exclude, unlike <see cref="NeighborsExcludingSelf(List{int},ValueTuple{int,int},int)" />).
    ///     Appends every character id in the 3x3 neighborhood of <paramref name="cell" /> to
    ///     <paramref name="buffer" />. Same "iterator + LINQ ToArray() per call" avoidance rationale as
    ///     <see cref="NeighborsExcludingSelf(List{int},ValueTuple{int,int},int)" /> -- <see cref="Zone.BroadcastMonsterAction" />
    ///     and <see cref="Zone.BroadcastGroundItemAction" /> call this once per due keep-alive, and during a
    ///     rebroadcast burst that is once per due entity in that single tick, not once per whole zone.
    /// </summary>
    public void Neighbors(List<int> buffer, (int X, int Z) cell)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    buffer.Add(id);
    }

    /// <summary>
    ///     Non-allocating, legacy-parity two-stage counterpart to <see cref="Neighbors(List{int},ValueTuple{int,int})" />:
    ///     same <c>+/-scale</c> box scan, plus the same exact-distance check documented on
    ///     <see cref="Neighbors(ValueTuple{int,int},float,float,float,int)" />. This is the overload every
    ///     production monster/ground-item/proxy-shop broadcast call site in <see cref="Zone" /> uses.
    /// </summary>
    public void Neighbors(List<int> buffer, (int X, int Z) cell, float originX, float originY, float originZ,
        int scale = 1)
    {
        var radiusSquared = ExactRadiusSquared(scale);
        for (var dx = -scale; dx <= scale; dx++)
        for (var dz = -scale; dz <= scale; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    if (WithinExactRadius(id, originX, originY, originZ, radiusSquared))
                        buffer.Add(id);
    }

    private float ExactRadiusSquared(int scale)
    {
        var radius = cellSize * scale;
        return radius * radius;
    }

    /// <summary>
    ///     Server/ts25zone/S07_MyGame03.cpp:838-844's exact-distance test, using <c>&lt;=</c> to admit where
    ///     legacy rejects on strictly-greater (:841) -- equivalent inclusion semantics. A member with no tracked
    ///     position (registered through a coarse-only <see cref="Add" />/<see cref="Move" /> overload, or --
    ///     defensively -- any other inconsistency) fails closed: treated as out of range rather than assumed
    ///     visible, since a broadcast recipient list must never silently include someone this grid cannot
    ///     actually place.
    /// </summary>
    private bool WithinExactRadius(int characterId, float originX, float originY, float originZ,
        float radiusSquared)
    {
        if (!_positions.TryGetValue(characterId, out var position))
            return false;

        var dx = position.X - originX;
        var dy = position.Y - originY;
        var dz = position.Z - originZ;
        return dx * dx + dy * dy + dz * dz <= radiusSquared;
    }
}
