using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.Runtime;

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

    private MonsterAiState _aiState = MonsterAiState.Spawning;

    internal Action<MonsterEntity>? PursuitStateChanged { get; set; }

    public MonsterAiState AiState
    {
        get => _aiState;
        set
        {
            _aiState = value;
            PursuitStateChanged?.Invoke(this);
        }
    }

    public int StateTicks { get; set; }

    public float StateFrameAccumulator { get; set; }

    public int? TargetCharacterId { get; private set; }

    public uint TargetUniqueNumber { get; private set; }

    public RuntimeIncarnation TargetIncarnation { get; private set; }

    public bool AttackPacketConfirmationArmed { get; private set; }

    public float AttackPacketConfirmationElapsedSeconds { get; private set; }

    public float TargetLocationX { get; set; }

    public float TargetLocationY { get; set; }

    public float TargetLocationZ { get; set; }

        public int DeathSkillNumber { get; private set; }

    public TimeSpan LastRebroadcastAt { get; set; }

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

    public void AssignTarget(int characterId, RuntimeIncarnation incarnation, uint uniqueNumber, float x, float y,
        float z)
    {
        TargetCharacterId = characterId;
        TargetIncarnation = incarnation;
        TargetUniqueNumber = uniqueNumber;
        TargetLocationX = x;
        TargetLocationY = y;
        TargetLocationZ = z;
        PursuitStateChanged?.Invoke(this);
    }

    public void ReleaseTarget()
    {
        TargetCharacterId = null;
        TargetIncarnation = default;
        TargetUniqueNumber = 0;
        ClearAttackPacketConfirmation();
        PursuitStateChanged?.Invoke(this);
    }

    public void ArmAttackPacketConfirmation()
    {
        AttackPacketConfirmationArmed = Template.AttackType is 1 or 2;
        AttackPacketConfirmationElapsedSeconds = 0f;
    }

    public void AdvanceAttackPacketConfirmation(float elapsedSeconds)
    {
        if (!AttackPacketConfirmationArmed || elapsedSeconds <= 0f)
            return;

        AttackPacketConfirmationElapsedSeconds += elapsedSeconds;
    }

    public void ClearAttackPacketConfirmation()
    {
        AttackPacketConfirmationArmed = false;
        AttackPacketConfirmationElapsedSeconds = 0f;
    }

    public void RecordDeathAction(int deathSkillNumber)
    {
        DeathSkillNumber = deathSkillNumber;
        AiState = MonsterAiState.Dead;
        StateTicks = 0;
        StateFrameAccumulator = 0f;
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
        var heading = rng.NextInt32(360);

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
            Heading = heading,
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

    internal void RegisterAttackDamage(int attackerCharacterId, RuntimeIncarnation incarnation, int damage)
    {
        if (damage <= 0)
            return;

        WriteAttackDamageSlot(attackerCharacterId, incarnation, damage);
    }

    internal void RegisterAcquisition(int characterId, RuntimeIncarnation incarnation)
    {
        WriteAttackDamageSlot(characterId, incarnation, 0);
    }

    private void WriteAttackDamageSlot(int characterId, RuntimeIncarnation incarnation, int damage)
    {
        lock (_attackDamageLock)
        {
            var existing = _attackDamage.Find(e =>
                e.CharacterId == characterId && e.Incarnation == incarnation);

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
                Incarnation = incarnation,
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
                    Incarnation = survivor.Incarnation,
                    CumulativeDamage = survivor.CumulativeDamage
                });
        }
    }
}

internal sealed class RandomSourceAdapter(Random random) : IRandomSource
{
    public int NextInt32(int exclusiveUpperBound)
    {
        return random.Next(exclusiveUpperBound);
    }
}
