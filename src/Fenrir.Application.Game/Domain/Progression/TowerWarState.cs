using System.Collections.ObjectModel;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Progression;

public enum TowerSiegePhase : byte
{
    Dormant,

    Building,

    Active,

    Sieged
}

public sealed class TowerWarState(ILogger<TowerWarState>? logger = null)
{
    public const int TowerCount = 12;

    public const int MaxTowersPerKind = 3;

    private const int AttackStateIdle = -1;

    private const int AttackStateUnderAttack = 0;

    private const int CollapsingStateCode = 9;

    public static readonly TimeSpan SiegeCollapseCooldown = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan CreateCooldown = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan AttackStateIdleReset = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan EngagementAutoClear = TimeSpan.FromMinutes(10);

    private readonly int[] _attackState = CreateIdleAttackStates();

    private readonly int[] _bonusInputScratch = new int[TowerCount];

    private readonly int[] _constructKind = new int[TowerCount];

    private readonly byte?[] _controllingTribe = new byte?[TowerCount];

    private readonly DateTime?[] _createCooldownStartedAtUtc = new DateTime?[TowerCount];

    private readonly bool[] _dirty = new bool[TowerCount];

    private readonly DateTime?[] _firstAttackAtUtc = new DateTime?[TowerCount];

    private readonly bool[] _firstAttackRecorded = new bool[TowerCount];

    private readonly bool[] _guardianHealPending = new bool[TowerCount];

    private readonly DateTime?[] _lastAttackAtUtc = new DateTime?[TowerCount];
    private readonly Lock _lock = new();
    private readonly int[] _packedState = new int[TowerCount];
    private readonly int[] _pendingPackedState = new int[TowerCount];
    private readonly byte?[] _pendingTribe = new byte?[TowerCount];
    private readonly DateTime?[] _siegeStartedAtUtc = new DateTime?[TowerCount];

    private readonly TowerTribeRewardBonus[] _tribeBonus = new TowerTribeRewardBonus[TowerRewardBonusTable.TribeCount];

    private readonly bool[] _valid = new bool[TowerCount];

    private static int[] CreateIdleAttackStates()
    {
        var values = new int[TowerCount];
        Array.Fill(values, AttackStateIdle);
        return values;
    }

    public int GetPackedState(int towerIndex)
    {
        lock (_lock)
        {
            return _packedState[towerIndex];
        }
    }

    public bool IsValid(int towerIndex)
    {
        lock (_lock)
        {
            return _valid[towerIndex];
        }
    }

    public TowerSiegePhase GetPhase(int towerIndex)
    {
        lock (_lock)
        {
            return PhaseOf(towerIndex);
        }
    }

    public byte? GetControllingTribe(int towerIndex)
    {
        lock (_lock)
        {
            return _controllingTribe[towerIndex];
        }
    }

    public TowerStatusResponse BuildStatusSnapshot()
    {
        var state1 = new int[TowerCount];
        var state2 = new int[TowerCount];

        lock (_lock)
        {
            for (var i = 0; i < TowerCount; i++)
            {
                state1[i] = _packedState[i];
                state2[i] = _attackState[i];
            }
        }

        return new TowerStatusResponse { State1Tower = state1, State2Tower = state2 };
    }

    public int GetPendingPackedStateForBuilding(int towerIndex)
    {
        lock (_lock)
        {
            return _pendingPackedState[towerIndex];
        }
    }

    public static int DecodeLevel(int packedState)
    {
        return packedState < 1 ? 0 : packedState / 100;
    }

    public static int DecodeType(int packedState)
    {
        return packedState < 1 ? 0 : packedState % 100;
    }

    public static int NormalizeToBuiltState(int stateCode)
    {
        return stateCode switch
        {
            1 or 2 => 2,
            3 or 4 => 4,
            5 or 6 => 6,
            7 or 8 => 8,
            _ => 0
        };
    }

    public void RecomputeTribeBonuses()
    {
        lock (_lock)
        {
            for (var i = 0; i < TowerCount; i++)
                _bonusInputScratch[i] = DecodeLevel(_packedState[i]) is 2 or 4 or 6 or 8 ? _packedState[i] : 0;

            var recomputed = TowerRewardBonusTable.Recompute(_bonusInputScratch);
            recomputed.CopyTo(_tribeBonus);
        }
    }

    public TowerTribeRewardBonus GetTribeBonus(byte tribe)
    {
        lock (_lock)
        {
            return tribe < _tribeBonus.Length ? _tribeBonus[tribe] : TowerTribeRewardBonus.None;
        }
    }

    public static int GuardianServerIndex(int towerIndex)
    {
        return -(towerIndex + 1);
    }

    public bool IsUnderAttack(int towerIndex)
    {
        lock (_lock)
        {
            return _attackState[towerIndex] == AttackStateUnderAttack;
        }
    }

    public bool IsEngagementWindowOpen(int towerIndex)
    {
        lock (_lock)
        {
            return _firstAttackRecorded[towerIndex];
        }
    }

    public DateTime? GetFirstAttackAtUtc(int towerIndex)
    {
        lock (_lock)
        {
            return _firstAttackAtUtc[towerIndex];
        }
    }

    public DateTime? GetLastAttackAtUtc(int towerIndex)
    {
        lock (_lock)
        {
            return _lastAttackAtUtc[towerIndex];
        }
    }

    public bool RecordGuardianHit(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            _attackState[towerIndex] = AttackStateUnderAttack;
            _lastAttackAtUtc[towerIndex] = utcNow;

            if (_firstAttackRecorded[towerIndex])
                return false;

            _firstAttackRecorded[towerIndex] = true;
            _firstAttackAtUtc[towerIndex] = utcNow;
            return true;
        }
    }

    public void SetTowerState(int towerIndex, int packedState, bool valid)
    {
        lock (_lock)
        {
            _packedState[towerIndex] = packedState;
            _valid[towerIndex] = valid;
        }
    }

    public void BeginUpgrade(int towerIndex, int newPackedState, byte controllingTribeId)
    {
        lock (_lock)
        {
            _valid[towerIndex] = false;
            _pendingPackedState[towerIndex] = newPackedState;
            _pendingTribe[towerIndex] = controllingTribeId;
        }
    }

    public void CompleteUpgrade(int towerIndex)
    {
        lock (_lock)
        {
            var pending = _pendingPackedState[towerIndex];
            if (pending <= 0)
                return;

            _packedState[towerIndex] = pending;
            _controllingTribe[towerIndex] = _pendingTribe[towerIndex];
            _pendingPackedState[towerIndex] = 0;
            _pendingTribe[towerIndex] = null;
            _valid[towerIndex] = true;
            _siegeStartedAtUtc[towerIndex] = null;
            _dirty[towerIndex] = true;

            _attackState[towerIndex] = AttackStateIdle;
            _firstAttackRecorded[towerIndex] = false;
            _firstAttackAtUtc[towerIndex] = null;
            _lastAttackAtUtc[towerIndex] = null;
        }
    }

    public void BeginSiege(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            if (_siegeStartedAtUtc[towerIndex] is not null)
                return;

            _packedState[towerIndex] = CollapsingStateCode * 100 + DecodeType(_packedState[towerIndex]);
            _attackState[towerIndex] = AttackStateUnderAttack;
            _valid[towerIndex] = false;
            _siegeStartedAtUtc[towerIndex] = utcNow;
        }
    }

    public bool IsDueForDestruction(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            return _siegeStartedAtUtc[towerIndex] is { } startedAt && utcNow - startedAt >= SiegeCollapseCooldown;
        }
    }

    public void CompleteDestruction(int towerIndex)
    {
        lock (_lock)
        {
            _packedState[towerIndex] = 0;
            _controllingTribe[towerIndex] = null;
            _siegeStartedAtUtc[towerIndex] = null;
            _valid[towerIndex] = false;
            _pendingPackedState[towerIndex] = 0;
            _pendingTribe[towerIndex] = null;
            _dirty[towerIndex] = true;

            _constructKind[towerIndex] = 0;
            _createCooldownStartedAtUtc[towerIndex] = null;
            _guardianHealPending[towerIndex] = false;
        }
    }


    public bool BeginConstruction(int towerIndex, int constructKind, byte controllingTribeId)
    {
        lock (_lock)
        {
            var idle = _packedState[towerIndex] == 0 && !_valid[towerIndex]
                                                     && _siegeStartedAtUtc[towerIndex] is null
                                                     && _pendingPackedState[towerIndex] == 0
                                                     && _constructKind[towerIndex] == 0;
            if (!idle)
                return false;

            _constructKind[towerIndex] = constructKind;
            _pendingTribe[towerIndex] = controllingTribeId;
            return true;
        }
    }

    public void CancelConstruction(int towerIndex)
    {
        lock (_lock)
        {
            if (_createCooldownStartedAtUtc[towerIndex] is not null)
                return;

            _constructKind[towerIndex] = 0;
            _pendingTribe[towerIndex] = null;
        }
    }

    public int GetPendingConstructKind(int towerIndex)
    {
        lock (_lock)
        {
            return _createCooldownStartedAtUtc[towerIndex] is null ? _constructKind[towerIndex] : 0;
        }
    }

    public void CompleteConstructionSpawn(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            var kind = _constructKind[towerIndex];
            if (kind == 0)
                return;

            _packedState[towerIndex] = 100 + kind;
            _controllingTribe[towerIndex] = _pendingTribe[towerIndex];
            _pendingTribe[towerIndex] = null;
            _constructKind[towerIndex] = 0;
            _createCooldownStartedAtUtc[towerIndex] = utcNow;
            _valid[towerIndex] = false;
            _dirty[towerIndex] = true;

            _attackState[towerIndex] = AttackStateIdle;
            _firstAttackRecorded[towerIndex] = false;
            _firstAttackAtUtc[towerIndex] = null;
            _lastAttackAtUtc[towerIndex] = null;
        }
    }

    public bool IsCreateCooldownElapsed(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            return _createCooldownStartedAtUtc[towerIndex] is { } startedAt && utcNow - startedAt >= CreateCooldown;
        }
    }

    public void CompleteConstructionCooldown(int towerIndex)
    {
        lock (_lock)
        {
            if (_createCooldownStartedAtUtc[towerIndex] is null)
                return;

            _createCooldownStartedAtUtc[towerIndex] = null;
            _packedState[towerIndex] = 200 + DecodeType(_packedState[towerIndex]);
            _attackState[towerIndex] = AttackStateIdle;
            _valid[towerIndex] = true;
            _dirty[towerIndex] = true;
        }
    }

    public int CountTowersOfKind(int kind)
    {
        lock (_lock)
        {
            var count = 0;
            for (var i = 0; i < TowerCount; i++)
            {
                var counted = DecodeLevel(_packedState[i]) is >= 1 and <= 8 &&
                              DecodeType(_packedState[i]) == kind;
                if (counted || _constructKind[i] == kind)
                    count++;
            }

            return count;
        }
    }

    public bool IsKindPresentInTribeGroup(int towerIndex, int kind)
    {
        lock (_lock)
        {
            var groupStart = towerIndex / 3 * 3;
            for (var i = groupStart; i < groupStart + 3 && i < TowerCount; i++)
            {
                if (i == towerIndex)
                    continue;

                if ((_packedState[i] > 0 && DecodeType(_packedState[i]) == kind) || _constructKind[i] == kind)
                    return true;
            }

            return false;
        }
    }

    public bool TryResetIdleAttackState(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            if (_lastAttackAtUtc[towerIndex] is not { } last || utcNow - last < AttackStateIdleReset)
                return false;

            _attackState[towerIndex] = AttackStateIdle;
            _lastAttackAtUtc[towerIndex] = utcNow;
            return true;
        }
    }

    public bool TryClearStaleEngagement(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            if (!_firstAttackRecorded[towerIndex] || _firstAttackAtUtc[towerIndex] is not { } first)
                return false;

            if (utcNow - first < EngagementAutoClear)
                return false;

            _firstAttackRecorded[towerIndex] = false;
            _firstAttackAtUtc[towerIndex] = null;
            return true;
        }
    }

    public void RequestGuardianHeal(int towerIndex)
    {
        lock (_lock)
        {
            _guardianHealPending[towerIndex] = true;
        }
    }

    public bool IsGuardianHealPending(int towerIndex)
    {
        lock (_lock)
        {
            return _guardianHealPending[towerIndex];
        }
    }

    public bool TryConsumeGuardianHeal(int towerIndex)
    {
        lock (_lock)
        {
            if (!_guardianHealPending[towerIndex])
                return false;

            _guardianHealPending[towerIndex] = false;
            return true;
        }
    }

    public async Task InitializeAsync(ITowerRepository towers, CancellationToken ct)
    {
        await towers.EnsureInitializedAsync(ct).ConfigureAwait(false);
        var rows = await towers.GetAllAsync(ct).ConfigureAwait(false);

        lock (_lock)
        {
            foreach (var row in rows)
            {
                if (row.TowerIndex >= TowerCount)
                    continue;

                _controllingTribe[row.TowerIndex] = row.ControllingTribeId;

                var builtState = NormalizeToBuiltState(row.Level);
                if (builtState > 0)
                {
                    _packedState[row.TowerIndex] = builtState * 100 + row.TowerType;
                    _valid[row.TowerIndex] = true;
                }
            }
        }
    }

    public async Task FlushDirtyAsync(ITowerRepository towers, CancellationToken ct)
    {
        for (var i = 0; i < TowerCount; i++)
        {
            int packed;
            byte? tribe;

            lock (_lock)
            {
                if (!_dirty[i])
                    continue;

                packed = _packedState[i];
                tribe = _controllingTribe[i];
                _dirty[i] = false;
            }

            try
            {
                var builtState = NormalizeToBuiltState(DecodeLevel(packed));
                await towers.SetProgressAsync((byte)i, (byte)builtState, (byte)DecodeType(packed), tribe, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _dirty[i] = true;
                }

                logger?.LogError(ex, "TowerState flush failed for tower {TowerIndex} -- will retry next interval", i);
            }
        }
    }

    public async Task ReconcileAsync(ITowerRepository towers, CancellationToken ct)
    {
        ReadOnlyCollection<TowerStateRowDto> rows;
        try
        {
            rows = await towers.GetAllAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "TowerState reconcile read failed -- will retry next interval");
            return;
        }

        lock (_lock)
        {
            foreach (var row in rows)
            {
                if (row.TowerIndex >= TowerCount)
                    continue;

                if (_dirty[row.TowerIndex] || _pendingPackedState[row.TowerIndex] > 0 ||
                    _siegeStartedAtUtc[row.TowerIndex] is not null ||
                    _createCooldownStartedAtUtc[row.TowerIndex] is not null)
                    continue;

                var builtState = NormalizeToBuiltState(row.Level);
                var packed = builtState > 0 ? builtState * 100 + row.TowerType : 0;

                _packedState[row.TowerIndex] = packed;
                _controllingTribe[row.TowerIndex] = row.ControllingTribeId;
                _valid[row.TowerIndex] = packed > 0;
            }
        }
    }

    private TowerSiegePhase PhaseOf(int towerIndex)
    {
        if (_siegeStartedAtUtc[towerIndex] is not null)
            return TowerSiegePhase.Sieged;
        if (_pendingPackedState[towerIndex] > 0)
            return TowerSiegePhase.Building;
        return _valid[towerIndex] ? TowerSiegePhase.Active : TowerSiegePhase.Dormant;
    }
}
