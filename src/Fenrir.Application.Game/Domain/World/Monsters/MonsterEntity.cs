using System.Numerics;
using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public sealed class MonsterEntity
{
    public const int TribeSymbolDamageSlots = 4;

    private const int MaxAttackDamageEntries = 50;

    private readonly List<MonsterAttackDamageEntry> _attackDamage = [];
    private readonly Lock _attackDamageLock = new();

    private readonly int[] _tribeSymbolDamage = new int[TribeSymbolDamageSlots];

    private int _deathClaimed;
    private int _life;

    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required MonsterRowDto Template { get; init; }

    public byte SpecialSort { get; init; }

    public required int SpawnSlotId { get; init; }

    public int? InstanceId { get; init; }

    public string? OwnerName { get; init; }

    public TimeSpan? OwnerNameLockExemptionArmedAt { get; init; }

    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Heading { get; set; }

    public (int X, int Z) CurrentCell { get; set; }

    public required float HomeX { get; init; }

    public required float HomeY { get; init; }
    public required float HomeZ { get; init; }

    public int MaxLife { get; init; }

    public MonsterAiState AiState { get; set; } = MonsterAiState.Spawning;

    public int StateTicks { get; set; }

    public float StateFrameAccumulator { get; set; }

    public int? TargetCharacterId { get; private set; }

    public uint TargetUniqueNumber { get; private set; }

    public bool AttackPacketConfirmationArmed { get; set; }

    public float TargetLocationX { get; set; }

    public float TargetLocationY { get; set; }

    public float TargetLocationZ { get; set; }

    public TimeSpan LastRebroadcastAt { get; set; }

    public List<Vector2> PathWaypoints { get; } = [];

    public int WaypointCursor { get; set; }

    public float PathGoalX { get; set; }

    public float PathGoalZ { get; set; }

    public int DetectionThrottleTicks { get; set; }

    public DateTime? LastCarThrowerDetectionCheckAtUtc { get; set; }

    public int IdleReturnElapsedTicks { get; set; }

    public int IdleWanderElapsedTicks { get; set; }

    public float WanderTargetX { get; set; }

    public float WanderTargetZ { get; set; }

    public float HomeReturnTargetX { get; set; }

    public float HomeReturnTargetY { get; set; }

    public float HomeReturnTargetZ { get; set; }

    public int PursuerCapacity { get; init; }

    public bool TribeSymbolFirstAttackArmed { get; set; }

    public bool AllianceStoneFirstAttackArmed { get; set; }

    public int TribeSymbolFirstAttackElapsedLegacyTicks { get; set; }

    public int AllianceStoneFirstAttackElapsedLegacyTicks { get; set; }

    public byte LastAttackerTribe { get; private set; }

    public string LastAttackerName { get; private set; } = string.Empty;

    public int Life => Volatile.Read(ref _life);

    public void RecordKillingBlowAttacker(byte tribe, string name)
    {
        LastAttackerTribe = tribe;
        LastAttackerName = name;
    }

    public void ClearPath()
    {
        PathWaypoints.Clear();
        WaypointCursor = 0;
    }

    public void AssignTarget(int characterId, uint uniqueNumber, float x, float y, float z)
    {
        TargetCharacterId = characterId;
        TargetUniqueNumber = uniqueNumber;
        TargetLocationX = x;
        TargetLocationY = y;
        TargetLocationZ = z;
    }

    public void ReleaseTarget()
    {
        TargetCharacterId = null;
        TargetUniqueNumber = 0;
    }

    public static MonsterEntity Create(int serverIndex, uint uniqueNumber, MonsterRowDto template, int spawnSlotId,
        float homeX, float homeY, float homeZ, int? instanceId = null,
        IRandomSource? random = null, byte? specialSort = null)
    {
        var rng = random ?? SystemRandomSource.Instance;

        var minPursuers = (int)template.FollowInfo1;
        var maxPursuers = (int)template.FollowInfo2;
        var pursuerCapacity = maxPursuers > minPursuers
            ? minPursuers + rng.NextInt32(maxPursuers - minPursuers + 1)
            : minPursuers;

        var entity = new MonsterEntity
        {
            ServerIndex = serverIndex,
            UniqueNumber = uniqueNumber,
            Template = template,
            SpawnSlotId = spawnSlotId,
            HomeX = homeX,
            HomeY = homeY,
            HomeZ = homeZ,
            MaxLife = template.Life,
            PosX = homeX,
            PosY = homeY,
            PosZ = homeZ,
            TargetLocationX = homeX,
            TargetLocationY = homeY,
            TargetLocationZ = homeZ,
            HomeReturnTargetX = homeX,
            HomeReturnTargetY = homeY,
            HomeReturnTargetZ = homeZ,
            InstanceId = instanceId,
            PursuerCapacity = pursuerCapacity,
            SpecialSort = specialSort ?? MonsterSpecialSort.Derive(template.Type, template.SpecialType)
        };
        entity._life = template.Life;
        return entity;
    }

    public bool TakeDamage(int amount, out int remainingLife)
    {
        if (amount < 0)
            amount = 0;

        int oldLife, newLife;
        do
        {
            oldLife = Volatile.Read(ref _life);
            if (oldLife <= 0)
            {
                remainingLife = 0;
                return false;
            }

            newLife = Math.Max(0, oldLife - amount);
        } while (Interlocked.CompareExchange(ref _life, newLife, oldLife) != oldLife);

        remainingLife = newLife;
        return newLife == 0 && Interlocked.CompareExchange(ref _deathClaimed, 1, 0) == 0;
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

        int oldLife, newLife;
        do
        {
            oldLife = Volatile.Read(ref _life);
            if (oldLife <= 0)
                return;

            newLife = Math.Min(MaxLife, oldLife + amount);
        } while (Interlocked.CompareExchange(ref _life, newLife, oldLife) != oldLife);
    }

    public void RestoreFullLife()
    {
        int oldLife;
        do
        {
            oldLife = Volatile.Read(ref _life);
            if (oldLife <= 0)
                return;
        } while (Interlocked.CompareExchange(ref _life, MaxLife, oldLife) != oldLife);
    }

    public void AddTribeSymbolDamage(byte tribe, int damage)
    {
        if (damage <= 0 || tribe >= TribeSymbolDamageSlots)
            return;

        Interlocked.Add(ref _tribeSymbolDamage[tribe], damage);
    }

    public void ResetTribeSymbolDamage()
    {
        for (var slot = 0; slot < _tribeSymbolDamage.Length; slot++)
            Interlocked.Exchange(ref _tribeSymbolDamage[slot], 0);
    }

    public bool TryResolveTribeSymbolWinner(out byte winnerTribe)
    {
        winnerTribe = 0;

        for (var candidate = 0; candidate < TribeSymbolDamageSlots; candidate++)
        {
            var value = Volatile.Read(ref _tribeSymbolDamage[candidate]);
            var strictlyAhead = true;

            for (var other = 0; other < TribeSymbolDamageSlots; other++)
            {
                if (other == candidate || value > Volatile.Read(ref _tribeSymbolDamage[other]))
                    continue;

                strictlyAhead = false;
                break;
            }

            if (!strictlyAhead)
                continue;

            winnerTribe = (byte)candidate;
            return true;
        }

        return false;
    }

    internal void RegisterAttackDamage(int attackerCharacterId, object sessionToken, int damage)
    {
        if (damage <= 0)
            return;

        WriteAttackDamageSlot(attackerCharacterId, sessionToken, damage);
    }

    internal void RegisterAcquisition(int characterId, object sessionToken)
    {
        WriteAttackDamageSlot(characterId, sessionToken, 0);
    }

    private void WriteAttackDamageSlot(int characterId, object sessionToken, int damage)
    {
        lock (_attackDamageLock)
        {
            var existing = _attackDamage.Find(e =>
                e.CharacterId == characterId && ReferenceEquals(e.SessionToken, sessionToken));

            if (existing is not null)
            {
                existing.CumulativeDamage += damage;
                return;
            }

            if (_attackDamage.Count >= MaxAttackDamageEntries)
                _attackDamage.RemoveAt(0);

            _attackDamage.Add(new MonsterAttackDamageEntry
            {
                CharacterId = characterId,
                SessionToken = sessionToken,
                CumulativeDamage = damage
            });
        }
    }

    internal IReadOnlyList<MonsterAttackDamageEntry> SnapshotAttackDamage()
    {
        lock (_attackDamageLock)
        {
            return _attackDamage.ToArray();
        }
    }

    internal bool HasTrackedAttackers()
    {
        lock (_attackDamageLock)
        {
            return _attackDamage.Count > 0;
        }
    }

    internal void ReplaceAttackDamage(IReadOnlyList<MonsterAggroListPruner.Survivor> survivors)
    {
        lock (_attackDamageLock)
        {
            _attackDamage.Clear();
            foreach (var survivor in survivors)
                _attackDamage.Add(new MonsterAttackDamageEntry
                {
                    CharacterId = survivor.CharacterId,
                    SessionToken = survivor.SessionToken,
                    CumulativeDamage = survivor.CumulativeDamage
                });
        }
    }
}
