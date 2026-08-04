using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.StellarCores;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class PositionWriteBehindHost : BackgroundService, ICharacterWriteBehindFlusher
{
    private const int BuffSlotCount = 35;

    private readonly ICharacterRepository _characters;
    private readonly SemaphoreSlim _disconnectRetrySignal = new(0, 1);

    private readonly PeriodicTimer _disconnectRetryTimer = new(WriteBehindFlusher<int>.DefaultInterval);
    private readonly WriteBehindFlusher<int> _flusher;

    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly ILogger<PositionWriteBehindHost> _logger;
    private readonly ICharacterLogoutStateRepository _logoutState;

    private readonly Dictionary<int, (CharacterProgressTvp Progress, CharacterPositionTvp Position,
            CharacterLogoutStateTvp LogoutState,
            List<CharacterCostumeSlotTvp> Costumes, List<CharacterBuffSlotTvp> Buffs,
            List<CharacterMountSlotTvp> Mounts, List<CharacterStellarCoreSlotTvp> StellarCores, PlayerRuntimeState
            CapturedState, PlayerLogoutSnapshot? CapturedLogoutSnapshot, int CapturedWarPoint, int CapturedBloodCoin,
            TaskCompletionSource Completion)>
        _pendingDisconnectRetries = new();

    private readonly ZoneRegistry _zones;

    public PositionWriteBehindHost(ZoneRegistry zones, DirtyTracker<int> dirtyTracker, ICharacterRepository characters,
        ICharacterLogoutStateRepository logoutState, ProgressWriteBehindHost progress,
        ILogger<PositionWriteBehindHost> logger)
    {
        _zones = zones;
        _characters = characters;
        _logoutState = logoutState;
        _logger = logger;

        _flusher = new WriteBehindFlusher<int>(
            dirtyTracker,
            async (dirty, ct) =>
            {
                await _flushGate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await progress.FlushAsync(dirty, ct).ConfigureAwait(false);

                    var rows = new List<CharacterPositionTvp>(dirty.Count);
                    var logoutRows = new List<CharacterLogoutStateTvp>(dirty.Count);
                    var capturedLogoutSnapshots = new List<(PlayerRuntimeState State, PlayerLogoutSnapshot Snapshot)>();

                    foreach (var (characterId, flags) in dirty)
                    {
                        if (!zones.TryGetPlayer(characterId, out var state))
                            continue;

                        var logoutSnapshot = state.LogoutSnapshot;
                        logoutRows.Add(CreateLogoutStateRow(state, logoutSnapshot));
                        if (logoutSnapshot is { } capturedSnapshot)
                            capturedLogoutSnapshots.Add((state, capturedSnapshot));

                        if ((flags & DirtyFlags.Position) == 0)
                            continue;

                        rows.Add(new CharacterPositionTvp(characterId, state.FlushSequence, state.MapId, state.PosX,
                            state.PosY, state.PosZ, state.Heading));
                    }

                    await characters.PersistPositionsAsync(rows, ct).ConfigureAwait(false);
                    await logoutState.PersistBatchAsync(logoutRows, ct).ConfigureAwait(false);

                    foreach (var (state, snapshot) in capturedLogoutSnapshots)
                        state.AcknowledgePersistedLogoutSnapshot(snapshot);
                }
                finally
                {
                    _flushGate.Release();
                }
            },
            onFlushError: ex => logger.LogError(ex, "Character write-behind flush failed (position and/or progress)"));
    }

    public void RequestImmediateFlush()
    {
        _flusher.RequestImmediateFlush();
        ReleaseDisconnectRetrySignal();
    }

    public async ValueTask<bool> FlushCharacterNowAsync(int characterId, CancellationToken ct,
        bool queueOnFailure = true)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_zones.TryGetPlayer(characterId, out var state))
            {
                _logger.LogDebug(
                    "FlushCharacterNowAsync({CharacterId}): character not found in any zone's live registry -- nothing to persist",
                    characterId);
                return false;
            }

            var persistence = await PersistFinalFlushAsync(state, ct, queueOnFailure).ConfigureAwait(false);
            return persistence.IsDurablyPersisted;
        }
        finally
        {
            _flushGate.Release();
        }
    }

    public async ValueTask<CharacterSnapshotPersistence> FlushCharacterSnapshotAsync(PlayerRuntimeState snapshot,
        CancellationToken ct)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await PersistFinalFlushAsync(snapshot, ct, true).ConfigureAwait(false);
        }
        finally
        {
            _flushGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _flusher.DisposeAsync().ConfigureAwait(false);
        _flushGate.Dispose();
        _disconnectRetryTimer.Dispose();
        _disconnectRetrySignal.Dispose();
    }

    private async ValueTask<CharacterSnapshotPersistence> PersistFinalFlushAsync(PlayerRuntimeState state, CancellationToken ct,
        bool queueOnFailure)
    {
        var characterId = state.CharacterId;
        var warPoint = state.WarPoint;
        var bloodCoin = state.BloodCoin;
        var logoutSnapshot = state.LogoutSnapshot;
        var logoutRow = CreateLogoutStateRow(state, logoutSnapshot);

        var progressRow = new CharacterProgressTvp(characterId, state.FlushSequence, state.Level, state.Level2,
            state.Experience, state.Life, state.MaxLife, state.Mana, state.MaxMana, state.StatVit, state.StatStr,
            state.StatInt, state.StatDex, state.StatPoints, state.SkillPoints, state.ContributionPoints,
            state.Exp2, state.RebirthCount, state.EatLifePotion, state.EatManaPotion, state.EatStrPotion,
            state.EatDexPotion, state.EatElePotion, state.DropItemTime, state.M15PetLuckyBoxPity,
            state.MountGarage[MountPersistenceCodec.PersistedGarageSlot],
            MountPersistenceCodec.EncodeExpActivity(state.MountActivity, state.MountAccumulatedExp),
            MountPersistenceCodec.EncodePower(state.MountRolledAttributes),
            state.AnimalIndex, state.AnimalTime,
            state.VisibleState, state.SpecialState, state.UseOrnament ? 1 : 0,
            state.Title, state.Halo, state.TeacherPoint,
            warPoint - state.PersistedWarPoint, bloodCoin - state.PersistedBloodCoin,
            state.PetExpX2Time, state.AnimalAbsorbTime, state.AnimalAbsorbState, state.CostumeIndex,
            state.ProtectForHalo,
            state.BonusItemLevel,
            state.BonusItemValue,
            state.TribeNotifyScrollCount,
            state.TribeFourReturnAllowance,
            BottleSlotsCodec.Encode(state.BottleSlots),
            state.DrunkBottleIndex,
            state.AutoBuffTime,
            AutoBuffSkillCodec.Encode(state.AutoBuffSkill),
            state.RankPointDate,
            state.RankBuffType,
            state.AutoHuntPaidDayBudget,
            state.AutoHuntPaidMinuteBudget,
            state.BuffX2Time,
            state.PremiumExpireUtc,
            state.PetGrowth,
            state.PetActivity,
            RankPoint: state.RankPoint,
            CloakLuckyBoxPity: state.CloakLuckyBoxPity,
            CloakVariantBoxPity: state.CloakVariantBoxPity,
            MountVariantBoxPity: state.MountVariantBoxPity,
            ImproveItemValue: state.ImproveItemValue,
            AddItemValue: state.AddItemValue,
            HighItemValue: state.HighItemValue,
            TaiyanKeyTimer: state.TaiyanKeyTimer,
            ProtectForRefine: state.ProtectForRefine,
            ProtectForDestroy: state.ProtectForDestroy,
            ProtectForCostume: state.ProtectForCostume,
            ProtectForDestroy2: state.ProtectForDestroy2,
            LodRounds: state.LodRounds,
            StellarCoreExpireDate: StellarCoreExpireDateCodec.Encode(state.StellarCoreExpireDate),
            EliteDungeonTime: state.EliteDungeonTime,
            DungeonKeyTime: state.DungeonKeyTime,
            IvyHallTicketTime: state.IvyHallTicketTime,
            ScrollOfSeekersTime: state.ScrollOfSeekersTime,
            FightingGodForDestroy: state.FightingGodForDestroy,
            PetBagDate: state.PetBagDate,
            PlayTime1: state.PlayTime1,
            PlayTime3: state.PlayTime3,
            HsbStoneRewardClaimed: state.HsbStoneRewardClaimed ? 1 : 0,
            TowerCpMilestoneCounter: state.TowerCpMilestoneCounter,
            InventoryDate: state.InventoryDate,
            StoreDate: state.StoreDate,
            WarriorPill: state.WarriorPill,
            WarriorScroll: state.WarriorScroll,
            SilverTime: state.SilverTime,
            GoldTime: state.GoldTime,
            DoubleKillNumTime: state.DoubleKillNumTime,
            DoubleKillExpTime: state.DoubleKillExpTime,
            DoubleKillNumTime2: state.DoubleKillNumTime2,
            ProtectForDeath: state.ProtectForDeath,
            AnimalDoubleExp: state.AnimalDoubleExp,
            DmgBoost: state.DmgBoost,
            HPBoost: state.HPBoost,
            CriBoost: state.CriBoost,
            StellarCoreIndex: state.StellarCoreIndex);

        var positionRow = new CharacterPositionTvp(characterId, state.FlushSequence, state.MapId, state.PosX,
            state.PosY, state.PosZ, state.Heading);

        var costumeRows = new List<CharacterCostumeSlotTvp>();
        CostumePersistenceCodec.AppendOccupiedSlots(costumeRows, characterId, state);

        var mountRows = new List<CharacterMountSlotTvp>();
        MountPersistenceCodec.AppendOccupiedSlots(mountRows, characterId, state);

        var stellarCoreRows = new List<CharacterStellarCoreSlotTvp>();
        StellarCorePersistenceCodec.AppendOccupiedSlots(stellarCoreRows, characterId, state);

        var buffRows = new List<CharacterBuffSlotTvp>();
        if (state.IsMovingZone)
            AppendOccupiedBuffSlots(buffRows, characterId, state);

        try
        {
            var flushResult = await _characters
                .PersistFinalFlushAsync(progressRow, positionRow, costumeRows, buffRows, mountRows, ct, stellarCoreRows)
                .ConfigureAwait(false);

            if (!flushResult.WasApplied && !flushResult.WasAlreadyApplied)
            {
                var queued = queueOnFailure
                    ? QueueDisconnectRetry(characterId, progressRow, positionRow, logoutRow, costumeRows, buffRows,
                        mountRows, stellarCoreRows, state, logoutSnapshot, warPoint, bloodCoin)
                    : null;
                _logger.LogError(
                    "Final flush for character {CharacterId} was rejected because a newer flush sequence is already durable",
                    characterId);
                return queued is null ? new CharacterSnapshotPersistence(Task.FromException(new InvalidOperationException(
                    $"Final flush for character {characterId} was rejected."))) : new CharacterSnapshotPersistence(queued.Task);
            }

            state.PersistedWarPoint = warPoint;
            state.PersistedBloodCoin = bloodCoin;

            try
            {
                await _logoutState.PersistBatchAsync([logoutRow], ct).ConfigureAwait(false);
                if (logoutSnapshot is { } capturedSnapshot)
                    state.AcknowledgePersistedLogoutSnapshot(capturedSnapshot);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && !queueOnFailure)
            {
                _logger.LogError(ex,
                    "Character {CharacterId} handoff state was persisted, but its auxiliary logout row could not be updated",
                    characterId);
            }

            return CharacterSnapshotPersistence.Persisted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var queued = queueOnFailure
                ? QueueDisconnectRetry(characterId, progressRow, positionRow, logoutRow, costumeRows, buffRows,
                    mountRows, stellarCoreRows, state, logoutSnapshot, warPoint, bloodCoin)
                : null;
            _logger.LogError(ex,
                queueOnFailure
                    ? "Failed to persist final Position/Vitals/Progression state for character {CharacterId} on disconnect -- queued for retry until it succeeds"
                    : "Failed to persist final Position/Vitals/Progression state for character {CharacterId} during zone handoff -- caller must roll back",
                characterId);
            return queued is null ? new CharacterSnapshotPersistence(Task.FromException(ex)) :
                new CharacterSnapshotPersistence(queued.Task);
        }
    }

    private TaskCompletionSource QueueDisconnectRetry(int characterId, CharacterProgressTvp progress,
        CharacterPositionTvp position, CharacterLogoutStateTvp logoutState,
        List<CharacterCostumeSlotTvp> costumes, List<CharacterBuffSlotTvp> buffs,
        List<CharacterMountSlotTvp> mounts, List<CharacterStellarCoreSlotTvp> stellarCores,
        PlayerRuntimeState state, PlayerLogoutSnapshot? logoutSnapshot, int warPoint, int bloodCoin)
    {
        if (_pendingDisconnectRetries.TryGetValue(characterId, out var existing))
            return existing.Completion;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingDisconnectRetries.Add(characterId,
            (progress, position, logoutState, costumes, buffs, mounts, stellarCores, state, logoutSnapshot, warPoint,
                bloodCoin, completion));
        ReleaseDisconnectRetrySignal();
        return completion;
    }

    private static void AppendOccupiedBuffSlots(List<CharacterBuffSlotTvp> destination, int characterId,
        PlayerRuntimeState state)
    {
        var buff = state.Buffs.Buff;

        for (var slot = 0; slot < BuffSlotCount; slot++)
        {
            var value = buff[slot * 2];
            var remainingTicks = buff[slot * 2 + 1];

            if (value == 0 && remainingTicks <= 0)
                continue;

            destination.Add(new CharacterBuffSlotTvp(characterId, (byte)slot, value, Math.Max(0, remainingTicks)));
        }
    }

    private static CharacterLogoutStateTvp CreateLogoutStateRow(PlayerRuntimeState state,
        PlayerLogoutSnapshot? snapshot)
    {
        return snapshot is { } captured
            ? new CharacterLogoutStateTvp(state.CharacterId, captured.FlushSequence, captured.MapId,
                captured.PosX, captured.PosY, captured.PosZ, captured.Life, captured.Mana)
            : new CharacterLogoutStateTvp(state.CharacterId, state.FlushSequence, state.MapId,
                (int)state.PosX, (int)state.PosY, (int)state.PosZ, state.Life, state.Mana);
    }

    private void ReleaseDisconnectRetrySignal()
    {
        try
        {
            _disconnectRetrySignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private async Task RetryPendingDisconnectFlushesAsync(CancellationToken ct)
    {
        try
        {
            var tickTask = _disconnectRetryTimer.WaitForNextTickAsync(ct).AsTask();
            var signalTask = _disconnectRetrySignal.WaitAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(tickTask, signalTask).ConfigureAwait(false);

                if (ct.IsCancellationRequested)
                    break;

                if (completed == tickTask)
                    tickTask = _disconnectRetryTimer.WaitForNextTickAsync(ct).AsTask();
                else
                    signalTask = _disconnectRetrySignal.WaitAsync(ct);

                await DrainPendingDisconnectRetriesAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await DrainPendingDisconnectRetriesAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async ValueTask DrainPendingDisconnectRetriesAsync(CancellationToken ct)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_pendingDisconnectRetries.Count == 0)
                return;

            foreach (var characterId in _pendingDisconnectRetries.Keys.ToArray())
            {
                var (progressRow, positionRow, logoutRow, costumeRows, buffRows, mountRows, stellarCoreRows,
                    capturedState, capturedLogoutSnapshot, capturedWarPoint, capturedBloodCoin, completion) =
                    _pendingDisconnectRetries[characterId];

                try
                {
                    var flushResult = await _characters
                        .PersistFinalFlushAsync(progressRow, positionRow, costumeRows, buffRows, mountRows, ct,
                            stellarCoreRows)
                        .ConfigureAwait(false);

                    if (!flushResult.WasApplied && !flushResult.WasAlreadyApplied)
                    {
                        _logger.LogError(
                            "Queued final flush for character {CharacterId} was rejected because a newer flush sequence is already durable",
                            characterId);
                        continue;
                    }

                    await _logoutState.PersistBatchAsync([logoutRow], ct).ConfigureAwait(false);
                    if (capturedLogoutSnapshot is { } snapshot)
                        capturedState.AcknowledgePersistedLogoutSnapshot(snapshot);

                    capturedState.PersistedWarPoint = Math.Max(capturedState.PersistedWarPoint, capturedWarPoint);
                    capturedState.PersistedBloodCoin = Math.Max(capturedState.PersistedBloodCoin, capturedBloodCoin);

                    _pendingDisconnectRetries.Remove(characterId);
                    completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Retry of the queued disconnect-time flush for character {CharacterId} failed again -- will retry next cycle",
                        characterId);
                }
            }
        }
        finally
        {
            _flushGate.Release();
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(_flusher.RunAsync(stoppingToken), RetryPendingDisconnectFlushesAsync(stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await _flusher.DisposeAsync().ConfigureAwait(false);
    }
}
