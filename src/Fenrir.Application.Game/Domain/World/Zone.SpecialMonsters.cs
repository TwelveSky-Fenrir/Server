using Fenrir.Application.Game.Domain.World.Monsters;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int SpecialMonsterPoolServerIndexBase = 3_900;

    private const int DungeonSpecialMonsterPoolServerIndexBase = 7_900;

    private const int SpecialMonsterPoolSize = 100;

    private const int TimedSpecialMonsterId = 9002;

    private static readonly TimeSpan TimedSpecialMonsterLifetime = TimeSpan.FromMinutes(3);

    private readonly Dictionary<int, SpecialMonsterExpiry> _specialMonsterExpiries = [];

    private int ActiveSpecialMonsterPoolServerIndexBase => IsDungeonServerZone
        ? DungeonSpecialMonsterPoolServerIndexBase
        : SpecialMonsterPoolServerIndexBase;

    private bool TrySummonSpecialMonster(int monsterId, float posX, float posY, float posZ,
        bool requireSingleLiveMonsterId)
    {
        if (requireSingleLiveMonsterId && SpecialMonsterAlreadyLive(monsterId))
            return false;

        if (!TryFindFreeSpecialMonsterSlot(out var serverIndex) ||
            !worldData.MonstersById.TryGetValue(monsterId, out var definition))
            return false;

        var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster, serverIndex,
            posX, posY, posZ);
        SpawnMonster(monster);
        TrackSpecialMonsterLifetime(monster);
        return true;
    }

    private bool SpecialMonsterAlreadyLive(int monsterId)
    {
        var poolServerIndexBase = ActiveSpecialMonsterPoolServerIndexBase;
        for (var i = 0; i < SpecialMonsterPoolSize; i++)
        {
            var candidate = poolServerIndexBase + i;
            if (_monsters.TryGetValue(candidate, out var monster) && monster.Template.MonsterId == monsterId)
                return true;
        }

        return false;
    }

    private bool TryFindFreeSpecialMonsterSlot(out int serverIndex)
    {
        var poolServerIndexBase = ActiveSpecialMonsterPoolServerIndexBase;
        if (poolServerIndexBase + SpecialMonsterPoolSize > MonsterServerIndexExclusiveForMap())
        {
            serverIndex = 0;
            return false;
        }

        for (var i = 0; i < SpecialMonsterPoolSize; i++)
        {
            var candidate = poolServerIndexBase + i;
            if (!_monsters.ContainsKey(candidate))
            {
                serverIndex = candidate;
                return true;
            }
        }

        serverIndex = 0;
        return false;
    }

    internal void ExpireSpecialMonsters()
    {
        List<int>? expiredServerIndexes = null;
        foreach (var (serverIndex, expiry) in _specialMonsterExpiries)
        {
            if (_clock < expiry.ExpiresAt)
                continue;

            if (_monsters.TryGetValue(serverIndex, out var monster) && monster.UniqueNumber == expiry.UniqueNumber)
                DespawnMonsterSilently(serverIndex);

            (expiredServerIndexes ??= []).Add(serverIndex);
        }

        if (expiredServerIndexes is null)
            return;

        foreach (var serverIndex in expiredServerIndexes)
            _specialMonsterExpiries.Remove(serverIndex);
    }

    private void TrackSpecialMonsterLifetime(MonsterEntity monster)
    {
        if (monster.Template.MonsterId != TimedSpecialMonsterId)
            return;

        _specialMonsterExpiries[monster.ServerIndex] = new SpecialMonsterExpiry(monster.UniqueNumber,
            _clock + TimedSpecialMonsterLifetime);
    }

    private readonly record struct SpecialMonsterExpiry(uint UniqueNumber, TimeSpan ExpiresAt);
}
