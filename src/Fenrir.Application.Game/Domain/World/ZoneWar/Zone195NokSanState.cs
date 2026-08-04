using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class Zone195NokSanState
{
    public const int StoneSlotCount = 9;
    public const int MaxStonesPerTribe = 4;
    public const int MonsterDamageBonusPerStone = 100;
    public const int MaximumAvatarNameLength = 13;
    public const int Server196StoneSlotIndex = 0;
    public const int Server99StoneSlotIndex = 2;
    public const int Server100StoneSlotIndex = 3;
    public const float DefaultCaptureRadius = 12.5f;
    public const float DefaultPostX = -20.0f;
    public const float DefaultPostZ = 2510.0f;

    public static readonly int TribeCount = WorldStateService.TribeCount;

    private readonly Lock _lock = new();
    private readonly Zone195CaptureMachine _capture99 = new();
    private readonly Zone195CaptureMachine _capture100 = new();
    private readonly Zone195CaptureMachine _capture196 = new();
    private readonly int[] _owners = new int[StoneSlotCount];
    private readonly int[] _stonesHeld = new int[TribeCount];
    private readonly int[] _publishedOwners = new int[StoneSlotCount];
    private readonly int[] _publishedStonesHeld = new int[TribeCount];

    private long _lastPersistedGeneration;
    private long _mutationGeneration;
    private long _revision;
    private Zone195NokSanDurableSnapshot _durableBaseline;
    private readonly List<Zone195NokSanConfirmedCapture> _confirmedCaptures = [];
    private bool _initialized;

    public bool IsInitialized
    {
        get
        {
            lock (_lock)
                return _initialized;
        }
    }

    public static bool IsValidSlot(int slotIndex) => slotIndex is >= 0 and < StoneSlotCount;

    public static bool IsActiveSlot(int slotIndex) => slotIndex is Server196StoneSlotIndex or Server99StoneSlotIndex
        or Server100StoneSlotIndex;

    public static bool IsValidTribe(int tribeId) => tribeId >= 0 && tribeId < TribeCount;

    public static bool IsActiveMapId(short mapId) => mapId is 99 or 100 or 196;

    public static bool HasExpectedSlot(short mapId, int slotIndex)
    {
        return (mapId, slotIndex) switch
        {
            (196, Server196StoneSlotIndex) => true,
            (99, Server99StoneSlotIndex) => true,
            (100, Server100StoneSlotIndex) => true,
            _ => false
        };
    }

    public int GetOwner(int slotIndex)
    {
        ValidateSlot(slotIndex);
        lock (_lock)
            return _publishedOwners[slotIndex];
    }

    public byte? GetOwningTribe(int slotIndex)
    {
        ValidateSlot(slotIndex);
        lock (_lock)
        {
            if (!_initialized || _publishedOwners[slotIndex] == 0)
                return null;

            return (byte)(_publishedOwners[slotIndex] - 1);
        }
    }

    public int GetStonesHeld(byte tribeId)
    {
        ValidateTribe(tribeId);
        lock (_lock)
            return _publishedStonesHeld[tribeId];
    }

    public int GetMonsterDamageBonus(byte tribeId)
    {
        return IsValidTribe(tribeId) ? GetStonesHeld(tribeId) * MonsterDamageBonusPerStone : 0;
    }

    public bool CommitCapture(int slotIndex, byte capturingTribe)
    {
        ValidateActiveSlot(slotIndex);
        ValidateTribe(capturingTribe);

        lock (_lock)
        {
            if (!_initialized)
                return false;

            if (_owners[slotIndex] == capturingTribe + 1)
                return true;

            _owners[slotIndex] = capturingTribe + 1;
            RecomputeStonesHeldUnsafe();
            _publishedOwners[slotIndex] = capturingTribe + 1;
            RecomputePublishedStonesHeldUnsafe();
            MarkChangedUnsafe();
            return true;
        }
    }

    public bool TryMutateCapture(short mapId, Func<Zone195CaptureMachine, bool> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ValidateActiveMap(mapId);

        lock (_lock)
        {
            if (!_initialized)
                return false;

            var machine = GetMachineUnsafe(mapId);
            var before = machine.Snapshot(mapId);
            mutation(machine);
            var after = machine.Snapshot(mapId);

            if (!after.HasExpectedShape)
            {
                machine.Restore(before);
                throw new ArgumentException("Nok-San capture mutations must leave a valid durable machine state.",
                    nameof(mutation));
            }

            if (after != before)
                MarkChangedUnsafe();

            return true;
        }
    }

    public bool TryGetCaptureSnapshot(short mapId, out Zone195NokSanCaptureSnapshot snapshot)
    {
        ValidateActiveMap(mapId);
        lock (_lock)
        {
            if (!_initialized)
            {
                snapshot = default;
                return false;
            }

            snapshot = GetMachineUnsafe(mapId).Snapshot(mapId);
            return true;
        }
    }

    public bool TryCompleteCapture(short mapId, int slotIndex, int expectedCapturerCharacterId,
        out Zone195NokSanCaptureSnapshot completedCapture, out Zone195NokSanStateSnapshot stateSnapshot)
    {
        ValidateActiveMap(mapId);
        if (!HasExpectedSlot(mapId, slotIndex))
            throw new ArgumentException("The Nok-San map does not own the specified stone slot.", nameof(slotIndex));

        lock (_lock)
        {
            if (!_initialized)
            {
                completedCapture = default;
                stateSnapshot = default;
                return false;
            }

            var machine = GetMachineUnsafe(mapId);
            if (machine.Phase != Zone195CapturePhase.Countdown || machine.RemainingTime != 0 ||
                machine.CapturerCharacterId != expectedCapturerCharacterId)
            {
                completedCapture = default;
                stateSnapshot = default;
                return false;
            }

            completedCapture = machine.Snapshot(mapId);
            if (!completedCapture.HasExpectedShape)
                throw new InvalidOperationException("Nok-San completed capture state is invalid.");

            if (_owners[slotIndex] == completedCapture.CapturerTribe + 1)
            {
                completedCapture = default;
                stateSnapshot = default;
                return false;
            }

            _owners[slotIndex] = completedCapture.CapturerTribe + 1;
            RecomputeStonesHeldUnsafe();
            MarkChangedUnsafe();
            stateSnapshot = SnapshotUnsafe();
            return true;
        }
    }

    public bool TryDequeueConfirmedCapture(short mapId, out Zone195NokSanConfirmedCapture confirmedCapture)
    {
        ValidateActiveMap(mapId);
        lock (_lock)
        {
            for (var index = 0; index < _confirmedCaptures.Count; index++)
            {
                if (_confirmedCaptures[index].MapId != mapId)
                    continue;

                confirmedCapture = _confirmedCaptures[index];
                _confirmedCaptures.RemoveAt(index);
                return true;
            }

            confirmedCapture = default;
            return false;
        }
    }

    public Zone195NokSanStateSnapshot Snapshot()
    {
        lock (_lock)
            return PublishedSnapshotUnsafe();
    }

    public bool TryGetDirtySnapshot(out Zone195NokSanDurableSnapshot snapshot)
    {
        lock (_lock)
        {
            if (!_initialized || _mutationGeneration == _lastPersistedGeneration)
            {
                snapshot = default;
                return false;
            }

            snapshot = DurableSnapshotUnsafe();
            return true;
        }
    }

    public void Initialize(in Zone195NokSanDurableSnapshot snapshot)
    {
        ValidateDurableSnapshot(snapshot);
        lock (_lock)
        {
            if (_initialized)
                throw new InvalidOperationException("Nok-San durable state has already been initialized.");

            ApplySnapshotUnsafe(snapshot);
            DiscardRecoveredCompletedCapturesUnsafe();
        }
    }

    public void AcknowledgePersisted(in Zone195NokSanDurableSnapshot snapshot)
    {
        ValidateDurableSnapshot(snapshot);
        lock (_lock)
        {
            if (!_initialized || snapshot.Revision != _revision || snapshot.Generation > _mutationGeneration)
                throw new InvalidOperationException("The acknowledged Nok-San snapshot is no longer current.");

            _revision = checked(snapshot.Revision + 1);
            _lastPersistedGeneration = snapshot.Generation;
            _durableBaseline = snapshot with { Revision = _revision, Generation = 0 };
            ApplyPublishedSnapshotUnsafe(snapshot.State);
            ConfirmPersistedCapturesUnsafe(snapshot);
        }
    }

    public void Reconcile(in Zone195NokSanDurableSnapshot snapshot)
    {
        ValidateDurableSnapshot(snapshot);
        lock (_lock)
        {
            if (!_initialized)
            {
                ApplySnapshotUnsafe(snapshot);
                DiscardRecoveredCompletedCapturesUnsafe();
                return;
            }

            var local = DurableSnapshotUnsafe();
            var baseline = _durableBaseline;
            ApplySnapshotUnsafe(snapshot);
            DiscardRecoveredCompletedCapturesUnsafe();
            MergePostAttemptChangesUnsafe(local, baseline);
        }
    }

    public void Reconcile(in Zone195NokSanDurableSnapshot snapshot,
        in Zone195NokSanDurableSnapshot persistenceAttempt)
    {
        ValidateDurableSnapshot(snapshot);
        ValidateDurableSnapshot(persistenceAttempt);

        lock (_lock)
        {
            if (!_initialized)
            {
                ApplySnapshotUnsafe(snapshot);
                DiscardRecoveredCompletedCapturesUnsafe();
                return;
            }

            var local = DurableSnapshotUnsafe();
            var baseline = persistenceAttempt.Revision == _revision ? persistenceAttempt : _durableBaseline;
            ApplySnapshotUnsafe(snapshot);
            DiscardRecoveredCompletedCapturesUnsafe();
            MergePostAttemptChangesUnsafe(local, baseline);
        }
    }

    public static Zone195NokSanDurableSnapshot CreateEmptySnapshot(long revision = 0)
    {
        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision));

        return new Zone195NokSanDurableSnapshot(
            revision,
            0,
            new Zone195NokSanStateSnapshot(ImmutableArray.CreateRange(new int[TribeCount]),
                ImmutableArray.CreateRange(new int[StoneSlotCount])),
            ImmutableArray.Create(
                IdleCapture(99),
                IdleCapture(100),
                IdleCapture(196)));
    }

    private static Zone195NokSanCaptureSnapshot IdleCapture(short mapId)
    {
        return new Zone195NokSanCaptureSnapshot(mapId, Zone195CapturePhase.IdleSearching,
            Zone195CaptureMachine.NoCapturer, 0, string.Empty, 0, 0);
    }

    private void ApplySnapshotUnsafe(in Zone195NokSanDurableSnapshot snapshot)
    {
        snapshot.State.Owners.CopyTo(_owners);
        snapshot.State.StonesHeld.CopyTo(_stonesHeld);
        ApplyPublishedSnapshotUnsafe(snapshot.State);
        GetMachineUnsafe(99).Restore(GetCapture(snapshot.Captures, 99));
        GetMachineUnsafe(100).Restore(GetCapture(snapshot.Captures, 100));
        GetMachineUnsafe(196).Restore(GetCapture(snapshot.Captures, 196));
        _revision = snapshot.Revision;
        _lastPersistedGeneration = 0;
        _mutationGeneration = 0;
        _initialized = true;
        _durableBaseline = snapshot with { Generation = 0 };
    }

    private void MergePostAttemptChangesUnsafe(in Zone195NokSanDurableSnapshot local,
        in Zone195NokSanDurableSnapshot persistenceAttempt)
    {
        var changed = false;

        foreach (var slot in ActiveSlots)
            if (local.State.Owners[slot] != persistenceAttempt.State.Owners[slot])
            {
                _owners[slot] = local.State.Owners[slot];
                changed = true;
            }

        if (changed)
            RecomputeStonesHeldUnsafe();

        foreach (var mapId in ActiveMapIds)
        {
            var localCapture = GetCapture(local.Captures, mapId);
            if (localCapture == GetCapture(persistenceAttempt.Captures, mapId))
                continue;

            GetMachineUnsafe(mapId).Restore(localCapture);
            changed = true;
        }

        if (changed)
        {
            _mutationGeneration = 1;
            _lastPersistedGeneration = 0;
        }
    }

    private Zone195NokSanDurableSnapshot DurableSnapshotUnsafe()
    {
        return new Zone195NokSanDurableSnapshot(
            _revision,
            _mutationGeneration,
            SnapshotUnsafe(),
            ImmutableArray.Create(
                _capture99.Snapshot(99),
                _capture100.Snapshot(100),
                _capture196.Snapshot(196)));
    }

    private Zone195NokSanStateSnapshot SnapshotUnsafe()
    {
        return new Zone195NokSanStateSnapshot([.. _stonesHeld], [.. _owners]);
    }

    private Zone195NokSanStateSnapshot PublishedSnapshotUnsafe()
    {
        return new Zone195NokSanStateSnapshot([.. _publishedStonesHeld], [.. _publishedOwners]);
    }

    private void RecomputeStonesHeldUnsafe()
    {
        Array.Clear(_stonesHeld);
        foreach (var slot in ActiveSlots)
            if (_owners[slot] != 0)
                _stonesHeld[_owners[slot] - 1]++;
    }

    private void RecomputePublishedStonesHeldUnsafe()
    {
        Array.Clear(_publishedStonesHeld);
        foreach (var slot in ActiveSlots)
            if (_publishedOwners[slot] != 0)
                _publishedStonesHeld[_publishedOwners[slot] - 1]++;
    }

    private void ApplyPublishedSnapshotUnsafe(in Zone195NokSanStateSnapshot snapshot)
    {
        snapshot.Owners.CopyTo(_publishedOwners);
        snapshot.StonesHeld.CopyTo(_publishedStonesHeld);
    }

    private void MarkChangedUnsafe()
    {
        _mutationGeneration = checked(_mutationGeneration + 1);
    }

    private void ConfirmPersistedCapturesUnsafe(in Zone195NokSanDurableSnapshot persistenceAttempt)
    {
        foreach (var capture in persistenceAttempt.Captures)
        {
            if (!IsCompletedCaptureUnsafe(capture, persistenceAttempt.State))
                continue;

            var machine = GetMachineUnsafe(capture.MapId);
            if (machine.Snapshot(capture.MapId) != capture)
                continue;

            _confirmedCaptures.Add(new Zone195NokSanConfirmedCapture(capture.MapId,
                capture.CapturerCharacterId, capture.CapturerTribe, capture.CapturerName, persistenceAttempt.State));
            machine.ResetToIdle();
            MarkChangedUnsafe();
        }
    }

    private void DiscardRecoveredCompletedCapturesUnsafe()
    {
        var state = SnapshotUnsafe();
        foreach (var mapId in ActiveMapIds)
        {
            var machine = GetMachineUnsafe(mapId);
            if (!IsCompletedCaptureUnsafe(machine.Snapshot(mapId), state))
                continue;

            machine.ResetToIdle();
            MarkChangedUnsafe();
        }
    }

    private static bool IsCompletedCaptureUnsafe(in Zone195NokSanCaptureSnapshot capture,
        in Zone195NokSanStateSnapshot state)
    {
        if (capture.Phase != Zone195CapturePhase.Countdown || capture.RemainingTime != 0)
            return false;

        return state.Owners[CaptureSlot(capture.MapId)] == capture.CapturerTribe + 1;
    }

    private static int CaptureSlot(short mapId)
    {
        return mapId switch
        {
            99 => Server99StoneSlotIndex,
            100 => Server100StoneSlotIndex,
            196 => Server196StoneSlotIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(mapId))
        };
    }

    private Zone195CaptureMachine GetMachineUnsafe(short mapId)
    {
        return mapId switch
        {
            99 => _capture99,
            100 => _capture100,
            196 => _capture196,
            _ => throw new ArgumentOutOfRangeException(nameof(mapId))
        };
    }

    private static Zone195NokSanCaptureSnapshot GetCapture(
        ImmutableArray<Zone195NokSanCaptureSnapshot> captures, short mapId)
    {
        foreach (var capture in captures)
            if (capture.MapId == mapId)
                return capture;

        throw new ArgumentException($"Nok-San map {mapId} is absent from the durable snapshot.", nameof(captures));
    }

    private static void ValidateDurableSnapshot(in Zone195NokSanDurableSnapshot snapshot)
    {
        if (!snapshot.HasExpectedShape)
            throw new ArgumentException("Nok-San durable state is structurally invalid.", nameof(snapshot));
    }

    private static void ValidateSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex,
                $"Zone195 stone slot must be 0-{StoneSlotCount - 1}.");
    }

    private static void ValidateActiveSlot(int slotIndex)
    {
        if (!IsActiveSlot(slotIndex))
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex,
                "Only slots 0, 2, and 3 are active Nok-San sites.");
    }

    private static void ValidateActiveMap(short mapId)
    {
        if (!IsActiveMapId(mapId))
            throw new ArgumentOutOfRangeException(nameof(mapId), mapId,
                "Only maps 99, 100, and 196 are active Nok-San sites.");
    }

    private static void ValidateTribe(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }

    private static ReadOnlySpan<int> ActiveSlots =>
    [Server196StoneSlotIndex, Server99StoneSlotIndex, Server100StoneSlotIndex];

    private static ReadOnlySpan<short> ActiveMapIds => [99, 100, 196];
}

public readonly record struct Zone195NokSanStateSnapshot(
    ImmutableArray<int> StonesHeld,
    ImmutableArray<int> Owners)
{
    public bool HasExpectedWireShape
    {
        get
        {
            if (StonesHeld.Length != Zone195NokSanState.TribeCount || Owners.Length != Zone195NokSanState.StoneSlotCount)
                return false;

            for (var tribeId = 0; tribeId < StonesHeld.Length; tribeId++)
                if (StonesHeld[tribeId] is < 0 or > Zone195NokSanState.MaxStonesPerTribe)
                    return false;

            for (var slot = 0; slot < Owners.Length; slot++)
                if (Owners[slot] < 0 || Owners[slot] > Zone195NokSanState.TribeCount ||
                    (!Zone195NokSanState.IsActiveSlot(slot) && Owners[slot] != 0))
                    return false;

            Span<int> expectedCounts = stackalloc int[Zone195NokSanState.TribeCount];
            for (var slot = 0; slot < Owners.Length; slot++)
                if (Owners[slot] != 0)
                    expectedCounts[Owners[slot] - 1]++;

            return StonesHeld.AsSpan().SequenceEqual(expectedCounts);
        }
    }
}

public readonly record struct Zone195NokSanDurableSnapshot(
    long Revision,
    long Generation,
    Zone195NokSanStateSnapshot State,
    ImmutableArray<Zone195NokSanCaptureSnapshot> Captures)
{
    public bool HasExpectedShape
    {
        get
        {
            if (Revision < 0 || Generation < 0 || !State.HasExpectedWireShape || Captures.Length != 3)
                return false;

            Span<bool> found = stackalloc bool[3];
            foreach (var capture in Captures)
            {
                if (!capture.HasExpectedShape)
                    return false;

                var index = capture.MapId switch
                {
                    99 => 0,
                    100 => 1,
                    196 => 2,
                    _ => -1
                };
                if (index < 0 || found[index])
                    return false;

                found[index] = true;
            }

            return found[0] && found[1] && found[2];
        }
    }
}

public readonly record struct Zone195NokSanConfirmedCapture(
    short MapId,
    int CapturerCharacterId,
    byte CapturerTribe,
    string CapturerName,
    Zone195NokSanStateSnapshot State);
