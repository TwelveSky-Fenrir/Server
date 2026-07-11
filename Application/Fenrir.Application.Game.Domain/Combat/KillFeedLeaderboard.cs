using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

public readonly record struct KillFeedRankedEntry(int CharacterId, string Name, byte Tribe, int Kills)
{
    public KillFeedTopEntry ToTopEntry()
    {
        return new KillFeedTopEntry(Name, Tribe, Kills);
    }
}

public sealed class KillFeedLeaderboard
{
    public const int Capacity = 1000;

    private readonly List<KillFeedRankedEntry> _entries = new(Capacity);

    public int Count => _entries.Count;

    public bool RecordKill(int characterId, string name, byte tribe, int killTotal)
    {
        var index = _entries.FindIndex(e => e.CharacterId == characterId);
        if (index < 0)
        {
            if (_entries.Count >= Capacity)
                return false;

            index = _entries.Count;
            _entries.Add(default);
        }

        _entries[index] = new KillFeedRankedEntry(characterId, name, tribe, killTotal);
        _entries.Sort(static (a, b) => b.Kills.CompareTo(a.Kills));
        return true;
    }

    public ImmutableArray<KillFeedRankedEntry> GetTopThree()
    {
        var count = Math.Min(3, _entries.Count);
        return count == 0 ? [] : [.. _entries.Take(count)];
    }

    public void Clear()
    {
        _entries.Clear();
    }
}
