namespace Fenrir.Application.Game.Domain.World;

public sealed class AoiGrid(float cellSize)
{
    private readonly Dictionary<(int X, int Z), HashSet<int>> _cells = new();

    private readonly Dictionary<int, (float X, float Y, float Z)> _positions = new();

    public (int X, int Z) CellOf(float posX, float posZ)
    {
        return ((int)MathF.Floor(posX / cellSize), (int)MathF.Floor(posZ / cellSize));
    }

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

    public bool HasAnyNeighbor((int X, int Z) cell, int scale = 1)
    {
        for (var dx = -scale; dx <= scale; dx++)
        for (var dz = -scale; dz <= scale; dz++)
            if (_cells.ContainsKey((cell.X + dx, cell.Z + dz)))
                return true;

        return false;
    }

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

    public void Neighbors(List<int> buffer, (int X, int Z) cell)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
            if (_cells.TryGetValue((cell.X + dx, cell.Z + dz), out var members))
                foreach (var id in members)
                    buffer.Add(id);
    }

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

    public bool IsWithinRadius(int characterId, float originX, float originY, float originZ, int scale)
    {
        return WithinExactRadius(characterId, originX, originY, originZ, ExactRadiusSquared(scale));
    }

    private float ExactRadiusSquared(int scale)
    {
        var radius = cellSize * scale;
        return radius * radius;
    }

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
