using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private sealed class PlayerRegistry : IEnumerable<KeyValuePair<int, PlayerRuntimeState>>
    {
        private readonly ConcurrentDictionary<int, PlayerRuntimeState> _players = new();

        private PlayerSnapshot _snapshot = PlayerSnapshot.Empty;

        public int Count => _players.Count;

        public bool IsEmpty => _players.IsEmpty;

        public ICollection<int> Keys => _players.Keys;

        public ICollection<PlayerRuntimeState> Values => _players.Values;

        public ImmutableArray<PlayerRuntimeState> OrderedSnapshot => Volatile.Read(ref _snapshot).Players;

        public PlayerRuntimeState this[int characterId]
        {
            get => _players[characterId];
            set
            {
                _players[characterId] = value;
                PublishReplacement(value);
            }
        }

        public bool ContainsKey(int characterId)
        {
            return _players.ContainsKey(characterId);
        }

        public IEnumerator<KeyValuePair<int, PlayerRuntimeState>> GetEnumerator()
        {
            return _players.GetEnumerator();
        }

        public bool TryAdd(int characterId, PlayerRuntimeState state)
        {
            if (!_players.TryAdd(characterId, state))
                return false;

            PublishAddition(state);
            return true;
        }

        public bool TryGetValue(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? state)
        {
            return _players.TryGetValue(characterId, out state);
        }

        public bool TryRemove(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? state)
        {
            if (!_players.TryRemove(characterId, out state))
                return false;

            PublishRemoval(characterId);
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void PublishAddition(PlayerRuntimeState state)
        {
            var current = OrderedSnapshot;
            var index = FindCharacterId(current, state.CharacterId);
            Volatile.Write(ref _snapshot, new PlayerSnapshot(current.Insert(~index, state)));
        }

        private void PublishRemoval(int characterId)
        {
            var current = OrderedSnapshot;
            var index = FindCharacterId(current, characterId);
            if (index < 0)
                return;

            Volatile.Write(ref _snapshot, new PlayerSnapshot(current.RemoveAt(index)));
        }

        private void PublishReplacement(PlayerRuntimeState state)
        {
            var current = OrderedSnapshot;
            var index = FindCharacterId(current, state.CharacterId);
            var updated = index >= 0 ? current.SetItem(index, state) : current.Insert(~index, state);
            Volatile.Write(ref _snapshot, new PlayerSnapshot(updated));
        }

        private static int FindCharacterId(ImmutableArray<PlayerRuntimeState> players, int characterId)
        {
            var lower = 0;
            var upper = players.Length - 1;

            while (lower <= upper)
            {
                var middle = lower + ((upper - lower) >> 1);
                var comparison = players[middle].CharacterId.CompareTo(characterId);

                if (comparison == 0)
                    return middle;

                if (comparison < 0)
                    lower = middle + 1;
                else
                    upper = middle - 1;
            }

            return ~lower;
        }

        private sealed class PlayerSnapshot(ImmutableArray<PlayerRuntimeState> players)
        {
            public static PlayerSnapshot Empty { get; } = new([]);

            public ImmutableArray<PlayerRuntimeState> Players { get; } = players;
        }
    }
}
